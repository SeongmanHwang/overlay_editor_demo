using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 채점 및 성적 처리 ViewModel입니다.
    /// </summary>
    public class GradingViewModel : INotifyPropertyChanged
    {
        private readonly NavigationViewModel _navigation;
        private readonly SessionStore _sessionStore;
        private readonly RegistryStore _registryStore;
        private readonly MarkingAnalyzer _markingAnalyzer;
        private readonly ScoringRuleStore _scoringRuleStore;
        private ObservableCollection<GradingResult>? _gradingResults;
        private ICollectionView? _filteredGradingResults;

        public GradingViewModel(NavigationViewModel navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _sessionStore = new SessionStore();
            _registryStore = new RegistryStore();
            _markingAnalyzer = new MarkingAnalyzer();
            _scoringRuleStore = new ScoringRuleStore();

            NavigateToHomeCommand = new RelayCommand(() => _navigation.NavigateTo(ApplicationMode.Home));

            LoadGradingData();
        }

        public ICommand NavigateToHomeCommand { get; }

        public NavigationViewModel Navigation => _navigation;

        public ObservableCollection<GradingResult>? GradingResults
        {
            get => _gradingResults;
            private set
            {
                _gradingResults = value;
                OnPropertyChanged();
                UpdateFilteredResults();
            }
        }

        public ICollectionView? FilteredGradingResults
        {
            get => _filteredGradingResults;
            private set
            {
                _filteredGradingResults = value;
                OnPropertyChanged();
            }
        }

        private void LoadGradingData()
        {
            try
            {
                Logger.Instance.Info("채점 데이터 로드 시작");

                // 1. Session에서 마킹 결과 로드
                var session = _sessionStore.Load();

                if (session.Documents == null || session.Documents.Count == 0)
                {
                    GradingResults = new ObservableCollection<GradingResult>();
                    Logger.Instance.Info("로드된 문서가 없습니다.");
                    return;
                }

                // 2. MarkingAnalyzer를 사용하여 SheetResults 생성
                var sheetResults = _markingAnalyzer.AnalyzeAllSheets(session);

                // 3. StudentRegistry 로드
                var studentRegistry = _registryStore.LoadStudentRegistry();

                // 4. ScoringRule 로드 (배점 정보)
                var scoringRule = _scoringRuleStore.LoadScoringRule();

                // 5. "수험번호 + 면접번호" 결합 ID 기준으로 그룹화하여 중복 감지
                var groupedByCombinedId = sheetResults
                    .Where(s => !string.IsNullOrEmpty(s.StudentId) && !string.IsNullOrEmpty(s.InterviewId))
                    .GroupBy(s => $"{s.StudentId}_{s.InterviewId}")
                    .ToDictionary(g => g.Key, g => g.ToList());

                // 6. GradingResult 생성
                var gradingResults = new ObservableCollection<GradingResult>();

                // 각 결합 ID별로 처리
                foreach (var combinedGroup in groupedByCombinedId)
                {
                    var combinedId = combinedGroup.Key;
                    var sheets = combinedGroup.Value;
                    var firstSheet = sheets.First();
                    var studentId = firstSheet.StudentId!;
                    var interviewId = firstSheet.InterviewId!;

                    // StudentInfo lookup
                    var studentInfo = studentRegistry.Students
                        .FirstOrDefault(s => s.StudentId == studentId);

                    // 중복 여부 확인 (같은 결합 ID로 여러 시트가 있으면 중복)
                    var hasDuplicate = sheets.Count > 1;
                    var duplicateCount = sheets.Count;

                    // 중복이 있는 경우 각 시트를 개별 행으로 표시
                    foreach (var sheet in sheets)
                    {
                        // 각 시트의 마킹 번호를 배점으로 변환
                        double? q1Score = null, q2Score = null, q3Score = null, q4Score = null;

                        if (sheet.Question1Marking.HasValue)
                        {
                            q1Score = scoringRule.GetScore(1, sheet.Question1Marking.Value);
                        }
                        if (sheet.Question2Marking.HasValue)
                        {
                            q2Score = scoringRule.GetScore(2, sheet.Question2Marking.Value);
                        }
                        if (sheet.Question3Marking.HasValue)
                        {
                            q3Score = scoringRule.GetScore(3, sheet.Question3Marking.Value);
                        }
                        if (sheet.Question4Marking.HasValue)
                        {
                            q4Score = scoringRule.GetScore(4, sheet.Question4Marking.Value);
                        }

                        var result = new GradingResult
                        {
                            ImageId = sheet.ImageId,
                            ImageFileName = string.Empty,
                            StudentId = studentId,
                            StudentName = studentInfo?.Name,
                            InterviewId = interviewId, // 중복 행 표시를 위해 면접번호 표시
                            Question1Marking = q1Score.HasValue ? (int?)Math.Round(q1Score.Value) : null,
                            Question2Marking = q2Score.HasValue ? (int?)Math.Round(q2Score.Value) : null,
                            Question3Marking = q3Score.HasValue ? (int?)Math.Round(q3Score.Value) : null,
                            Question4Marking = q4Score.HasValue ? (int?)Math.Round(q4Score.Value) : null,
                            IsDuplicate = hasDuplicate,
                            DuplicateCount = duplicateCount,
                            HasErrors = sheet.HasErrors,
                            RegistrationNumber = studentInfo?.RegistrationNumber,
                            ExamType = studentInfo?.ExamType,
                            MiddleSchool = studentInfo?.MiddleSchool,
                            BirthDate = studentInfo?.BirthDate
                        };

                        CalculateScores(result);
                        gradingResults.Add(result);
                    }
                }

                // 중복이 없는 시트들도 처리 (StudentId는 있지만 InterviewId가 없는 경우, 또는 결합 ID가 1개만 있는 경우)
                var processedCombinedIds = groupedByCombinedId.Keys.ToHashSet();
                var remainingSheets = sheetResults
                    .Where(s => !string.IsNullOrEmpty(s.StudentId) && 
                                (string.IsNullOrEmpty(s.InterviewId) || 
                                 !processedCombinedIds.Contains($"{s.StudentId}_{s.InterviewId}")))
                    .ToList();

                // InterviewId가 없는 시트들 처리
                var sheetsWithoutInterviewId = sheetResults
                    .Where(s => !string.IsNullOrEmpty(s.StudentId) && string.IsNullOrEmpty(s.InterviewId))
                    .ToList();

                foreach (var sheet in sheetsWithoutInterviewId)
                {
                    var studentId = sheet.StudentId!;
                    var studentInfo = studentRegistry.Students
                        .FirstOrDefault(s => s.StudentId == studentId);

                    double? q1Score = null, q2Score = null, q3Score = null, q4Score = null;

                    if (sheet.Question1Marking.HasValue)
                    {
                        q1Score = scoringRule.GetScore(1, sheet.Question1Marking.Value);
                    }
                    if (sheet.Question2Marking.HasValue)
                    {
                        q2Score = scoringRule.GetScore(2, sheet.Question2Marking.Value);
                    }
                    if (sheet.Question3Marking.HasValue)
                    {
                        q3Score = scoringRule.GetScore(3, sheet.Question3Marking.Value);
                    }
                    if (sheet.Question4Marking.HasValue)
                    {
                        q4Score = scoringRule.GetScore(4, sheet.Question4Marking.Value);
                    }

                    var result = new GradingResult
                    {
                        ImageId = sheet.ImageId,
                        ImageFileName = string.Empty,
                        StudentId = studentId,
                        StudentName = studentInfo?.Name,
                        InterviewId = null,
                        Question1Marking = q1Score.HasValue ? (int?)Math.Round(q1Score.Value) : null,
                        Question2Marking = q2Score.HasValue ? (int?)Math.Round(q2Score.Value) : null,
                        Question3Marking = q3Score.HasValue ? (int?)Math.Round(q3Score.Value) : null,
                        Question4Marking = q4Score.HasValue ? (int?)Math.Round(q4Score.Value) : null,
                        IsDuplicate = false,
                        DuplicateCount = 1,
                        HasErrors = sheet.HasErrors,
                        RegistrationNumber = studentInfo?.RegistrationNumber,
                        ExamType = studentInfo?.ExamType,
                        MiddleSchool = studentInfo?.MiddleSchool,
                        BirthDate = studentInfo?.BirthDate
                    };

                    CalculateScores(result);
                    gradingResults.Add(result);
                }

                // StudentId가 없는 오류 데이터는 별도로 처리
                var errorSheets = sheetResults
                    .Where(s => string.IsNullOrEmpty(s.StudentId))
                    .ToList();

                foreach (var errorSheet in errorSheets)
                {
                    // 오류 데이터도 배점으로 변환
                    double? q1Score = null, q2Score = null, q3Score = null, q4Score = null;
                    
                    if (errorSheet.Question1Marking.HasValue)
                    {
                        q1Score = scoringRule.GetScore(1, errorSheet.Question1Marking.Value);
                    }
                    if (errorSheet.Question2Marking.HasValue)
                    {
                        q2Score = scoringRule.GetScore(2, errorSheet.Question2Marking.Value);
                    }
                    if (errorSheet.Question3Marking.HasValue)
                    {
                        q3Score = scoringRule.GetScore(3, errorSheet.Question3Marking.Value);
                    }
                    if (errorSheet.Question4Marking.HasValue)
                    {
                        q4Score = scoringRule.GetScore(4, errorSheet.Question4Marking.Value);
                    }

                    var errorResult = new GradingResult
                    {
                        ImageId = errorSheet.ImageId,
                        ImageFileName = string.Empty,
                        StudentId = null,
                        InterviewId = errorSheet.InterviewId,
                        Question1Marking = q1Score.HasValue ? (int?)Math.Round(q1Score.Value) : null,
                        Question2Marking = q2Score.HasValue ? (int?)Math.Round(q2Score.Value) : null,
                        Question3Marking = q3Score.HasValue ? (int?)Math.Round(q3Score.Value) : null,
                        Question4Marking = q4Score.HasValue ? (int?)Math.Round(q4Score.Value) : null,
                        HasErrors = errorSheet.HasErrors
                    };
                    CalculateScores(errorResult);
                    gradingResults.Add(errorResult);
                }

                // 7. 석차 계산 (TotalScore 기준)
                CalculateRank(gradingResults);

                GradingResults = gradingResults;

                Logger.Instance.Info($"채점 데이터 로드 완료: {gradingResults.Count}개 항목");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("채점 데이터 로드 실패", ex);
                GradingResults = new ObservableCollection<GradingResult>();
            }
        }

        private void CalculateScores(GradingResult result)
        {
            var scores = new System.Collections.Generic.List<double>();
            if (result.Question1Marking.HasValue) scores.Add(result.Question1Marking.Value);
            if (result.Question2Marking.HasValue) scores.Add(result.Question2Marking.Value);
            if (result.Question3Marking.HasValue) scores.Add(result.Question3Marking.Value);
            if (result.Question4Marking.HasValue) scores.Add(result.Question4Marking.Value);

            result.TotalScore = scores.Count > 0 ? scores.Sum() : (double?)null;
            result.AverageScore = scores.Count > 0 ? scores.Average() : (double?)null;
        }

        private void CalculateRank(ObservableCollection<GradingResult> results)
        {
            // TotalScore 기준으로 정렬하여 석차 계산
            var sorted = results
                .OrderByDescending(r => r.TotalScore ?? 0)
                .ThenByDescending(r => r.AverageScore ?? 0)
                .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].Rank = i + 1;
            }
        }

        private void UpdateFilteredResults()
        {
            if (GradingResults == null)
            {
                FilteredGradingResults = null;
                return;
            }

            var view = CollectionViewSource.GetDefaultView(GradingResults);
            FilteredGradingResults = view;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
