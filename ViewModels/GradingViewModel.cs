using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Services.Mappers;

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
        private readonly IQuestionResultMapper<OmrSheetResult> _sheetMapper;
        private readonly IQuestionResultMapper<GradingResult> _gradingMapper;
        private ObservableCollection<GradingResult>? _gradingResults;
        private ICollectionView? _filteredGradingResults;
        private bool _hasMismatch;
        private int _missingInGradingCount;
        private int _missingInRegistryCount;
        private string? _mismatchMessage;
        private string? _missingInGradingList;
        private string? _missingInRegistryList;

        /// <summary>
        /// 기본 생성자 (기본 구현 사용)
        /// </summary>
        public GradingViewModel(NavigationViewModel navigation)
            : this(navigation, new OmrSheetResultMapper(), new GradingResultMapper())
        {
        }

        /// <summary>
        /// 테스트/확장을 위한 생성자 (의존성 주입)
        /// </summary>
        private GradingViewModel(
            NavigationViewModel navigation,
            IQuestionResultMapper<OmrSheetResult> sheetMapper,
            IQuestionResultMapper<GradingResult> gradingMapper)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _sheetMapper = sheetMapper ?? throw new ArgumentNullException(nameof(sheetMapper));
            _gradingMapper = gradingMapper ?? throw new ArgumentNullException(nameof(gradingMapper));
            
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
                _userHasSorted = false;   // 새 데이터 로드 = 기본 정렬 다시 적용 가능
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

        /// <summary>
        /// 수험번호 불일치 여부
        /// </summary>
        public bool HasMismatch
        {
            get => _hasMismatch;
            private set
            {
                _hasMismatch = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 명렬에는 있지만 채점 결과에 없는 수험번호 개수
        /// </summary>
        public int MissingInGradingCount
        {
            get => _missingInGradingCount;
            private set
            {
                _missingInGradingCount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 채점 결과에는 있지만 명렬에 없는 수험번호 개수
        /// </summary>
        public int MissingInRegistryCount
        {
            get => _missingInRegistryCount;
            private set
            {
                _missingInRegistryCount = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 불일치 메시지
        /// </summary>
        public string? MismatchMessage
        {
            get => _mismatchMessage;
            private set
            {
                _mismatchMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 명렬에는 있지만 채점 결과에 없는 수험번호 목록
        /// </summary>
        public string? MissingInGradingList
        {
            get => _missingInGradingList;
            private set
            {
                _missingInGradingList = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 채점 결과에는 있지만 명렬에 없는 수험번호 목록
        /// </summary>
        public string? MissingInRegistryList
        {
            get => _missingInRegistryList;
            private set
            {
                _missingInRegistryList = value;
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
                    // 불일치 정보 초기화
                    HasMismatch = false;
                    MissingInGradingCount = 0;
                    MissingInRegistryCount = 0;
                    MismatchMessage = null;
                    MissingInGradingList = null;
                    MissingInRegistryList = null;
                    Logger.Instance.Info("로드된 문서가 없습니다.");
                    return;
                }

                // 2. MarkingAnalyzer를 사용하여 SheetResults 생성
                var sheetResults = _markingAnalyzer.AnalyzeAllSheets(session);

                // 3. StudentRegistry 로드
                var studentRegistry = _registryStore.LoadStudentRegistry();

                // 4. ScoringRule 로드 (배점 정보)
                var scoringRule = _scoringRuleStore.LoadScoringRule();

                // 5. 수험번호별로 그룹화
                var groupedByStudentId = sheetResults
                    .Where(s => !string.IsNullOrEmpty(s.StudentId))
                    .GroupBy(s => s.StudentId!)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // 6. GradingResult 생성
                var gradingResults = new ObservableCollection<GradingResult>();

                // 각 수험번호별로 처리
                foreach (var studentGroup in groupedByStudentId)
                {
                    var studentId = studentGroup.Key;
                    var studentSheets = studentGroup.Value; // 이 수험번호의 모든 시트

                    // StudentInfo lookup
                    var studentInfo = studentRegistry.Students
                        .FirstOrDefault(s => s.StudentId == studentId);

                    // 면접위원 수 확인
                    int interviewerCount = studentSheets.Count;
                    bool hasInterviewerCountError = interviewerCount != 3; // 1, 2, 4 이상이면 오류

                    // "수험번호 + 면접번호" 결합 ID 기준으로 중복 확인
                    var combinedIdGroups = studentSheets
                        .Where(s => !string.IsNullOrEmpty(s.InterviewId))
                        .GroupBy(s => $"{s.StudentId}_{s.InterviewId}")
                        .ToList();

                    bool hasDuplicate = false;
                    int duplicateCount = 0;
                    foreach (var combinedGroup in combinedIdGroups)
                    {
                        if (combinedGroup.Count() > 1)
                        {
                            hasDuplicate = true;
                            duplicateCount += combinedGroup.Count();
                        }
                    }

                    // 면접위원별 점수를 문항별로 평균 계산 - 반복문으로 개선
                    var questionSums = new double[OmrConstants.QuestionsCount];
                    var questionCounts = new int[OmrConstants.QuestionsCount];
                    bool hasErrors = false;

                    foreach (var sheet in studentSheets)
                    {
                        // 각 시트의 마킹 번호를 배점으로 변환하여 합산 - Mapper 사용
                        for (int q = 1; q <= OmrConstants.QuestionsCount; q++)
                        {
                            var marking = _sheetMapper.GetQuestionMarking(sheet, q);
                            if (marking.HasValue)
                            {
                                var score = scoringRule.GetScore(q, marking.Value);
                                questionSums[q - 1] += score;
                                questionCounts[q - 1]++;
                            }
                        }

                        if (sheet.HasErrors) hasErrors = true;
                    }

                    // 하나의 GradingResult만 생성
                    var result = new GradingResult
                    {
                        StudentId = studentId,
                        StudentName = studentInfo?.Name,
                        InterviewId = null, // 평균이므로 면접번호는 표시하지 않음
                        IsDuplicate = hasDuplicate,
                        DuplicateCount = duplicateCount, // 중복이 없으면 0, 있으면 중복된 개수
                        HasErrors = hasErrors || hasInterviewerCountError, // 면접위원 수 오류도 포함
                        RegistrationNumber = studentInfo?.RegistrationNumber,
                        ExamType = studentInfo?.ExamType,
                        MiddleSchool = studentInfo?.MiddleSchool,
                        BirthDate = studentInfo?.BirthDate
                    };

                    // 평균 계산 및 Mapper를 통한 설정
                    for (int q = 1; q <= OmrConstants.QuestionsCount; q++)
                    {
                        var avg = questionCounts[q - 1] > 0
                            ? questionSums[q - 1] / questionCounts[q - 1]
                            : (double?)null;
                        _gradingMapper.SetQuestionMarking(result, q,
                            avg.HasValue ? (int?)Math.Round(avg.Value) : null);
                    }

                    CalculateScores(result);
                    gradingResults.Add(result);
                }

                // 7. 수험번호 비교 검사
                CheckStudentIdMismatch(gradingResults, studentRegistry);

                // 8. 석차 계산 (TotalScore 기준)
                CalculateRank(gradingResults);

                // 9. GradingResults 설정 (이때 UpdateFilteredResults가 호출되어 기본 정렬이 자동 적용됨)
                GradingResults = gradingResults;

                Logger.Instance.Info($"채점 데이터 로드 완료: {gradingResults.Count}개 항목");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("채점 데이터 로드 실패", ex);
                GradingResults = new ObservableCollection<GradingResult>();
                // 불일치 정보 초기화
                HasMismatch = false;
                MissingInGradingCount = 0;
                MissingInRegistryCount = 0;
                MismatchMessage = null;
                MissingInGradingList = null;
                MissingInRegistryList = null;
            }
        }

        private void CalculateScores(GradingResult result)
        {
            var scores = new System.Collections.Generic.List<double>();

            // Mapper를 통한 반복 접근
            foreach (var qNum in _gradingMapper.GetAllQuestionNumbers())
            {
                var marking = _gradingMapper.GetQuestionMarking(result, qNum);
                if (marking.HasValue) scores.Add(marking.Value);
            }

            result.TotalScore = scores.Count > 0 ? scores.Sum() : (double?)null;
            result.AverageScore = scores.Count > 0 ? scores.Average() : (double?)null;
        }

        private void CheckStudentIdMismatch(ObservableCollection<GradingResult> gradingResults, StudentRegistry studentRegistry)
        {
            var registryStudentIds = studentRegistry.Students.Select(s => s.StudentId).ToHashSet();
            var gradingStudentIds = gradingResults.Where(r => !string.IsNullOrEmpty(r.StudentId))
                                                  .Select(r => r.StudentId!)
                                                  .ToHashSet();

            // 명렬에는 있지만 채점에는 없는 수험번호
            var missingInGrading = registryStudentIds.Except(gradingStudentIds).ToList();

            // 채점에는 있지만 명렬에는 없는 수험번호
            var missingInRegistry = gradingStudentIds.Except(registryStudentIds).ToList();

            // 명렬에 없는 수험번호는 오류로 표시
            foreach (var result in gradingResults)
            {
                if (!string.IsNullOrEmpty(result.StudentId) && !registryStudentIds.Contains(result.StudentId))
                {
                    result.HasErrors = true; // 명렬에 없는 수험번호는 오류
                }
            }

            // UI 표시를 위한 정보 저장
            MissingInGradingCount = missingInGrading.Count;
            MissingInRegistryCount = missingInRegistry.Count;
            HasMismatch = missingInGrading.Any() || missingInRegistry.Any();

            if (HasMismatch)
            {
                var messages = new System.Collections.Generic.List<string>();
                if (missingInGrading.Any())
                {
                    // 수험번호 목록 생성 (최대 20개까지 표시)
                    var studentIdList = missingInGrading.Take(20).ToList();
                    var studentIdText = string.Join(", ", studentIdList);
                    if (missingInGrading.Count > 20)
                    {
                        studentIdText += $" 외 {missingInGrading.Count - 20}개";
                    }
                    messages.Add($"명렬에는 있지만 채점 결과에 없는 수험번호: {missingInGrading.Count}개");
                    MissingInGradingList = $"수험번호: {studentIdText}";
                }
                else
                {
                    MissingInGradingList = null;
                }

                if (missingInRegistry.Any())
                {
                    // 수험번호 목록 생성 (최대 20개까지 표시)
                    var studentIdList = missingInRegistry.Take(20).ToList();
                    var studentIdText = string.Join(", ", studentIdList);
                    if (missingInRegistry.Count > 20)
                    {
                        studentIdText += $" 외 {missingInRegistry.Count - 20}개";
                    }
                    messages.Add($"채점 결과에는 있지만 명렬에 없는 수험번호: {missingInRegistry.Count}개");
                    MissingInRegistryList = $"수험번호: {studentIdText}";
                }
                else
                {
                    MissingInRegistryList = null;
                }

                MismatchMessage = string.Join("\n", messages);
            }
            else
            {
                MismatchMessage = null;
                MissingInGradingList = null;
                MissingInRegistryList = null;
            }

            // 로그 출력
            if (missingInGrading.Any())
            {
                Logger.Instance.Warning($"명렬에는 있지만 채점 결과에 없는 수험번호 ({missingInGrading.Count}개): {string.Join(", ", missingInGrading.Take(10))}" + 
                    (missingInGrading.Count > 10 ? "..." : ""));
            }
            if (missingInRegistry.Any())
            {
                Logger.Instance.Warning($"채점 결과에는 있지만 명렬에 없는 수험번호 ({missingInRegistry.Count}개): {string.Join(", ", missingInRegistry.Take(10))}" + 
                    (missingInRegistry.Count > 10 ? "..." : ""));
            }
        }

        private void CalculateRank(ObservableCollection<GradingResult> results)
        {
            // 전형명별로 그룹화하여 각 전형명 내에서 석차 계산
            var groupedByExamType = results
                .Where(r => !string.IsNullOrEmpty(r.ExamType) && r.TotalScore.HasValue)
                .GroupBy(r => r.ExamType!)
                .ToList();

            // 전형명이 없는 경우도 처리
            var withoutExamType = results
                .Where(r => string.IsNullOrEmpty(r.ExamType) || !r.TotalScore.HasValue)
                .ToList();

            // 각 전형명별로 석차 계산
            foreach (var group in groupedByExamType)
            {
                var examType = group.Key;
                var students = group
                    .OrderByDescending(r => r.TotalScore ?? 0)
                    .ThenByDescending(r => r.AverageScore ?? 0)
                    .ToList();

                int currentRank = 1;
                int index = 0;

                while (index < students.Count)
                {
                    var currentScore = students[index].TotalScore ?? 0;
                    
                    // 같은 점수를 가진 학생들 찾기
                    var sameScoreStudents = students
                        .Skip(index)
                        .TakeWhile(r => (r.TotalScore ?? 0) == currentScore)
                        .ToList();
                    
                    int sameScoreCount = sameScoreStudents.Count;
                    
                    // 동점자들에게 같은 석차 부여
                    foreach (var student in sameScoreStudents)
                    {
                        student.Rank = currentRank;
                    }
                    
                    // 다음 석차는 동점자 수만큼 밀림
                    currentRank += sameScoreCount;
                    index += sameScoreCount;
                }
            }

            // 전형명이 없거나 점수가 없는 경우 석차를 null로 설정
            foreach (var result in withoutExamType)
            {
                result.Rank = null;
            }
        }

        private bool _userHasSorted = false; // 사용자가 정렬을 변경했는지 추적

        /// <summary>
        /// 사용자가 정렬을 변경했음을 표시합니다 (View에서 호출)
        /// </summary>
        public void MarkUserHasSorted()
        {
            _userHasSorted = true;
        }

        private void UpdateFilteredResults()
        {
            if (GradingResults == null)
            {
                FilteredGradingResults = null;
                return;
            }

            var view = CollectionViewSource.GetDefaultView(GradingResults);
            
            // 기본 정렬 적용: 사용자가 정렬을 변경하지 않은 경우에만
            // 컬렉션이 교체될 때마다 기본 정렬을 다시 적용
            if (!_userHasSorted)
            {
                view.SortDescriptions.Clear();
                // 중복이 가장 위, 그 다음 오류
                view.SortDescriptions.Add(new SortDescription("IsDuplicate", ListSortDirection.Descending));
                view.SortDescriptions.Add(new SortDescription("HasErrors", ListSortDirection.Descending));
                // 그 다음 전형 순, 석차 순, 접수번호 순
                view.SortDescriptions.Add(new SortDescription("ExamType", ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription("Rank", ListSortDirection.Ascending));
                view.SortDescriptions.Add(new SortDescription("RegistrationNumber", ListSortDirection.Ascending));
                view.Refresh();
            }
            
            FilteredGradingResults = view;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
