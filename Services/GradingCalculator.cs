using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services.Mappers;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 성적처리(채점 결과) 계산/조회 서비스입니다.
    /// - ViewModel 생성 부작용(전체 로딩)을 제거하기 위해 계산을 서비스로 이동합니다.
    /// - 내부 캐시를 통해 Grading/검산 간 중복 계산을 줄입니다.
    /// </summary>
    public sealed class GradingCalculator
    {
        private static readonly Lazy<GradingCalculator> _instance = new(() => new GradingCalculator(OmrAnalysisCache.Instance));
        public static GradingCalculator Instance => _instance.Value;

        private readonly OmrAnalysisCache _cache;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private string _roundKey = "";
        private ObservableCollection<GradingResult>? _allResults;
        private Dictionary<string, GradingResult>? _byStudentId;
        private GradingComputationSummary? _summary;

        private readonly IQuestionResultMapper<OmrSheetResult> _sheetMapper = new OmrSheetResultMapper();
        private readonly IQuestionScoreMapper<GradingResult> _gradingScoreMapper = new GradingResultScoreMapper();

        private GradingCalculator(OmrAnalysisCache cache)
        {
            _cache = cache;
        }

        private static string GetCurrentRoundKey() => PathService.CurrentRound ?? "";

        private void InvalidateIfRoundChanged_NoLock()
        {
            var current = GetCurrentRoundKey();
            if (!string.Equals(_roundKey, current, StringComparison.Ordinal))
            {
                _roundKey = current;
                _allResults = null;
                _byStudentId = null;
                _summary = null;
            }
        }

        public async Task InvalidateAsync()
        {
            await _gate.WaitAsync();
            try
            {
                _roundKey = GetCurrentRoundKey();
                _allResults = null;
                _byStudentId = null;
                _summary = null;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<(ObservableCollection<GradingResult> Results, GradingComputationSummary Summary)> GetAllAsync()
        {
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();
                if (_allResults != null && _summary != null)
                {
                    return (_allResults, _summary);
                }

                var session = await _cache.GetSessionAsync();
                var sheetResults = await _cache.GetAllSheetResultsAsync();
                var studentRegistry = await _cache.GetStudentRegistryAsync();
                var scoringRule = await _cache.GetScoringRuleAsync();

                var computed = await Task.Run(() => ComputeAll(session, sheetResults, studentRegistry, scoringRule));

                _allResults = computed.Results;
                _summary = computed.Summary;
                _byStudentId = computed.ByStudentId;

                return (_allResults, _summary);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<GradingResult?> GetByStudentIdAsync(string studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId)) return null;
            var id = studentId.Trim();

            // fast path if already computed
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();
                if (_byStudentId != null && _byStudentId.TryGetValue(id, out var cached))
                {
                    return cached;
                }
            }
            finally
            {
                _gate.Release();
            }

            // compute just this student using subset sheetResults when possible
            var studentRegistry = await _cache.GetStudentRegistryAsync();
            var scoringRule = await _cache.GetScoringRuleAsync();
            var sheets = await _cache.GetSheetResultsForStudentAsync(id);
            if (sheets.Count == 0) return null;

            var result = await Task.Run(() => ComputeForStudent(id, sheets, studentRegistry, scoringRule));

            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();
                _byStudentId ??= new Dictionary<string, GradingResult>(StringComparer.Ordinal);
                _byStudentId[id] = result;
            }
            finally
            {
                _gate.Release();
            }

            return result;
        }

        public async Task<IReadOnlyDictionary<string, GradingResult>> GetByStudentIdsAsync(IEnumerable<string> studentIds)
        {
            var ids = studentIds
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (ids.Count == 0) return new Dictionary<string, GradingResult>();

            // if all computed, just return slice
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();
                if (_byStudentId != null && ids.All(id => _byStudentId.ContainsKey(id)))
                {
                    return ids.ToDictionary(id => id, id => _byStudentId[id], StringComparer.Ordinal);
                }
            }
            finally
            {
                _gate.Release();
            }

            // compute missing (subset per student)
            var studentRegistry = await _cache.GetStudentRegistryAsync();
            var scoringRule = await _cache.GetScoringRuleAsync();

            var dict = new Dictionary<string, GradingResult>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var result = await GetByStudentIdAsync(id);
                if (result != null) dict[id] = result;
            }

            return dict;
        }

        public async Task<IReadOnlyList<string>> GetRandomSampleStudentIdsAsync(int count, int seed)
        {
            if (count <= 0) return Array.Empty<string>();

            // Prefer barcode-only index for speed
            var idx = await _cache.GetStudentIdIndexByImageIdAsync();
            var allStudentIds = idx.Values
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            if (allStudentIds.Count == 0) return Array.Empty<string>();

            var rng = new Random(seed);
            return allStudentIds
                .OrderBy(_ => rng.Next())
                .Take(Math.Min(count, allStudentIds.Count))
                .ToList();
        }

        private record ComputeAllResult(
            ObservableCollection<GradingResult> Results,
            Dictionary<string, GradingResult> ByStudentId,
            GradingComputationSummary Summary);

        private ComputeAllResult ComputeAll(
            Session session,
            List<OmrSheetResult> sheetResults,
            StudentRegistry studentRegistry,
            ScoringRule scoringRule)
        {
            // NOTE: sheetResults는 cache 단계에서 DuplicateDetector 적용됨

            var groupedByStudentId = sheetResults
                .Where(s => !string.IsNullOrEmpty(s.StudentId))
                .GroupBy(s => s.StudentId!)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var gradingResults = new ObservableCollection<GradingResult>();

            foreach (var studentGroup in groupedByStudentId)
            {
                var studentId = studentGroup.Key;
                var studentSheets = studentGroup.Value;

                gradingResults.Add(ComputeForStudent(studentId, studentSheets, studentRegistry, scoringRule));
            }

            // mismatch/랭크/요약은 전체 필요
            var summary = BuildSummaryAndMutateResults(gradingResults, studentRegistry, sheetResults);

            var byId = new Dictionary<string, GradingResult>(StringComparer.Ordinal);
            foreach (var r in gradingResults)
            {
                if (!string.IsNullOrWhiteSpace(r.StudentId))
                {
                    byId[r.StudentId!] = r;
                }
            }

            return new ComputeAllResult(gradingResults, byId, summary);
        }

        private GradingResult ComputeForStudent(
            string studentId,
            List<OmrSheetResult> studentSheets,
            StudentRegistry studentRegistry,
            ScoringRule scoringRule)
        {
            var studentInfo = studentRegistry.Students.FirstOrDefault(s => s.StudentId == studentId);

            int interviewerCount = studentSheets.Count;
            bool hasInterviewerCountError = interviewerCount <= 2;

            bool hasDuplicate = studentSheets.Any(s => s.IsDuplicate);
            int duplicateCount = studentSheets.Count(s => s.IsDuplicate);

            var questionRawSums = new int[OmrConstants.QuestionsCount];
            var questionCounts = new int[OmrConstants.QuestionsCount];
            bool hasSimpleErrors = false;

            foreach (var sheet in studentSheets)
            {
                for (int q = 1; q <= OmrConstants.QuestionsCount; q++)
                {
                    var marking = _sheetMapper.GetQuestionMarking(sheet, q);
                    if (marking.HasValue)
                    {
                        var score = scoringRule.GetScore(q, marking.Value);
                        questionRawSums[q - 1] += score;
                        questionCounts[q - 1]++;
                    }
                }

                if (sheet.IsSimpleError) hasSimpleErrors = true;
            }

            var result = new GradingResult
            {
                StudentId = studentId,
                StudentName = studentInfo?.Name,
                InterviewId = null,
                IsDuplicate = hasDuplicate,
                DuplicateCount = duplicateCount,
                IsSimpleError = hasSimpleErrors || hasInterviewerCountError,
                RegistrationNumber = studentInfo?.RegistrationNumber,
                ExamType = studentInfo?.ExamType,
                MiddleSchool = studentInfo?.MiddleSchool,
                BirthDate = studentInfo?.BirthDate
            };

            result.TotalScoreRaw = questionRawSums.Sum();

            for (int q = 1; q <= OmrConstants.QuestionsCount; q++)
            {
                var avg = questionCounts[q - 1] > 0
                    ? (double)questionRawSums[q - 1] / questionCounts[q - 1]
                    : (double?)null;
                _gradingScoreMapper.SetQuestionScore(result, q, avg);
            }

            CalculateScores(result);
            return result;
        }

        private void CalculateScores(GradingResult result)
        {
            var scores = new List<double>();
            foreach (var qNum in _gradingScoreMapper.GetAllQuestionNumbers())
            {
                var score = _gradingScoreMapper.GetQuestionScore(result, qNum);
                if (score.HasValue) scores.Add(score.Value);
            }

            result.TotalScore = scores.Count > 0 ? scores.Sum() : (double?)null;
            result.AverageScore = scores.Count > 0 ? scores.Average() : (double?)null;
        }

        private static void CalculateRank(ObservableCollection<GradingResult> results)
        {
            var groupedByExamType = results
                .Where(r => !string.IsNullOrEmpty(r.ExamType) && r.TotalScoreRaw.HasValue)
                .GroupBy(r => r.ExamType!)
                .ToList();

            var withoutExamType = results
                .Where(r => string.IsNullOrEmpty(r.ExamType) || !r.TotalScoreRaw.HasValue)
                .ToList();

            foreach (var group in groupedByExamType)
            {
                var students = group
                    .OrderByDescending(r => r.TotalScoreRaw ?? 0)
                    .ThenByDescending(r => r.TotalScore ?? 0)
                    .ToList();

                int currentRank = 1;
                int index = 0;
                while (index < students.Count)
                {
                    var currentScore = students[index].TotalScoreRaw ?? 0;
                    var sameScoreStudents = students
                        .Skip(index)
                        .TakeWhile(r => (r.TotalScoreRaw ?? 0) == currentScore)
                        .ToList();

                    foreach (var student in sameScoreStudents)
                    {
                        student.Rank = currentRank;
                    }

                    currentRank += sameScoreStudents.Count;
                    index += sameScoreStudents.Count;
                }
            }

            foreach (var result in withoutExamType)
            {
                result.Rank = null;
            }
        }

        private static GradingComputationSummary BuildSummaryAndMutateResults(
            ObservableCollection<GradingResult> gradingResults,
            StudentRegistry studentRegistry,
            List<OmrSheetResult> sheetResults)
        {
            // mismatch
            var registryStudentIds = studentRegistry.Students.Select(s => s.StudentId).ToHashSet(StringComparer.Ordinal);
            var gradingStudentIds = gradingResults.Where(r => !string.IsNullOrEmpty(r.StudentId))
                                                  .Select(r => r.StudentId!)
                                                  .ToHashSet(StringComparer.Ordinal);

            var missingInGrading = registryStudentIds.Except(gradingStudentIds).ToList();
            var missingInRegistry = gradingStudentIds.Except(registryStudentIds).ToList();

            foreach (var result in gradingResults)
            {
                if (!string.IsNullOrEmpty(result.StudentId) && !registryStudentIds.Contains(result.StudentId))
                {
                    result.IsSimpleError = true;
                    result.ErrorDetails = string.IsNullOrEmpty(result.ErrorDetails)
                        ? "명렬에 없음"
                        : result.ErrorDetails + ", 명렬에 없음";
                }
            }

            bool hasMismatch = missingInGrading.Any() || missingInRegistry.Any();
            string? mismatchMessage = null;
            string? missingInGradingList = null;
            string? missingInRegistryList = null;

            if (hasMismatch)
            {
                var messages = new List<string>();
                if (missingInGrading.Any())
                {
                    var studentIdList = missingInGrading.Take(20).ToList();
                    var studentIdText = string.Join(", ", studentIdList);
                    if (missingInGrading.Count > 20) studentIdText += $" 외 {missingInGrading.Count - 20}개";
                    messages.Add($"명렬에는 있지만 채점 결과에 없는 수험번호: {missingInGrading.Count}개");
                    missingInGradingList = $"수험번호: {studentIdText}";
                }

                if (missingInRegistry.Any())
                {
                    var studentIdList = missingInRegistry.Take(20).ToList();
                    var studentIdText = string.Join(", ", studentIdList);
                    if (missingInRegistry.Count > 20) studentIdText += $" 외 {missingInRegistry.Count - 20}개";
                    messages.Add($"채점 결과에는 있지만 명렬에 없는 수험번호: {missingInRegistry.Count}개");
                    missingInRegistryList = $"수험번호: {studentIdText}";
                }

                mismatchMessage = string.Join("\n", messages);
            }

            // rank
            CalculateRank(gradingResults);

            // counts (sheetResults 기준)
            int totalSheetCount = sheetResults.Count;
            int errorSheetCount = sheetResults.Count(r => r.HasErrors);
            int duplicateCombinedIdCount = sheetResults.Count(r => r.IsDuplicate);
            int nullCombinedIdCount = sheetResults.Count(r => string.IsNullOrEmpty(r.CombinedId));

            // lists (GradingResults 정렬 기준으로)
            var sortedResults = gradingResults
                .OrderByDescending(r => r.IsDuplicate)
                .ThenByDescending(r => r.IsSimpleError)
                .ThenBy(r => r.ExamType ?? "")
                .ThenBy(r => r.Rank ?? int.MaxValue)
                .ThenBy(r => r.RegistrationNumber ?? "")
                .ToList();

            string? errorSheetList = null;
            var errorSheetStudentIds = sortedResults
                .Where(r => r.HasErrors && !string.IsNullOrEmpty(r.StudentId))
                .Select(r => r.StudentId!)
                .Distinct()
                .ToList();
            if (errorSheetStudentIds.Any())
            {
                errorSheetList = $" (수험번호: {string.Join(", ", errorSheetStudentIds.Take(20))}" +
                                (errorSheetStudentIds.Count > 20 ? $" 외 {errorSheetStudentIds.Count - 20}개)" : ")");
            }

            string? duplicateCombinedIdList = null;
            var duplicateStudentIds = sortedResults
                .Where(r => r.IsDuplicate && !string.IsNullOrEmpty(r.StudentId))
                .Select(r => r.StudentId!)
                .Distinct()
                .ToList();
            if (duplicateStudentIds.Any())
            {
                duplicateCombinedIdList = $" (수험번호: {string.Join(", ", duplicateStudentIds.Take(20))}" +
                                         (duplicateStudentIds.Count > 20 ? $" 외 {duplicateStudentIds.Count - 20}개)" : ")");
            }

            string? nullCombinedIdList = null;
            var nullCombinedIdStudentIds = sheetResults
                .Where(r => string.IsNullOrEmpty(r.CombinedId) && !string.IsNullOrEmpty(r.StudentId))
                .Select(r => r.StudentId!)
                .Distinct()
                .ToList();
            if (nullCombinedIdStudentIds.Any())
            {
                var sortedNullCombinedIds = sortedResults
                    .Where(r => nullCombinedIdStudentIds.Contains(r.StudentId ?? ""))
                    .Select(r => r.StudentId!)
                    .Distinct()
                    .ToList();
                var remainingNullIds = nullCombinedIdStudentIds
                    .Except(sortedNullCombinedIds)
                    .OrderBy(id => id)
                    .ToList();
                sortedNullCombinedIds.AddRange(remainingNullIds);

                nullCombinedIdList = $" (수험번호: {string.Join(", ", sortedNullCombinedIds.Take(20))}" +
                                    (sortedNullCombinedIds.Count > 20 ? $" 외 {sortedNullCombinedIds.Count - 20}개)" : ")");
            }

            return new GradingComputationSummary(
                TotalSheetCount: totalSheetCount,
                ErrorSheetCount: errorSheetCount,
                DuplicateCombinedIdCount: duplicateCombinedIdCount,
                NullCombinedIdCount: nullCombinedIdCount,
                ErrorSheetList: errorSheetList,
                DuplicateCombinedIdList: duplicateCombinedIdList,
                NullCombinedIdList: nullCombinedIdList,
                HasMismatch: hasMismatch,
                MissingInGradingCount: missingInGrading.Count,
                MissingInRegistryCount: missingInRegistry.Count,
                MismatchMessage: mismatchMessage,
                MissingInGradingList: missingInGradingList,
                MissingInRegistryList: missingInRegistryList);
        }
    }

    public record GradingComputationSummary(
        int TotalSheetCount,
        int ErrorSheetCount,
        int DuplicateCombinedIdCount,
        int NullCombinedIdCount,
        string? ErrorSheetList,
        string? DuplicateCombinedIdList,
        string? NullCombinedIdList,
        bool HasMismatch,
        int MissingInGradingCount,
        int MissingInRegistryCount,
        string? MismatchMessage,
        string? MissingInGradingList,
        string? MissingInRegistryList);
}

