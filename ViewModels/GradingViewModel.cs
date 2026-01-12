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
        private ObservableCollection<GradingResult>? _gradingResults;
        private ICollectionView? _filteredGradingResults;

        public GradingViewModel(NavigationViewModel navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _sessionStore = new SessionStore();
            _registryStore = new RegistryStore();
            _markingAnalyzer = new MarkingAnalyzer();

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

                // 4. 수험번호 기준으로 그룹화하여 면접위원별 점수를 합산
                var groupedByStudentId = sheetResults
                    .Where(s => !string.IsNullOrEmpty(s.StudentId))
                    .GroupBy(s => s.StudentId!)
                    .ToList();

                // 5. 수험번호별로 GradingResult 생성 (면접위원 점수 합산)
                var gradingResults = new ObservableCollection<GradingResult>();

                foreach (var studentGroup in groupedByStudentId)
                {
                    var studentId = studentGroup.Key;
                    var studentSheets = studentGroup.ToList();

                    // StudentInfo lookup
                    var studentInfo = studentRegistry.Students
                        .FirstOrDefault(s => s.StudentId == studentId);

                    // "수험번호 + 면접번호" 결합 ID 기준으로 중복 감지
                    // 같은 면접위원(면접번호)에 여러 시트가 있으면 중복
                    var groupedByCombinedId = studentSheets
                        .Where(s => !string.IsNullOrEmpty(s.InterviewId))
                        .GroupBy(s => $"{s.StudentId}_{s.InterviewId}")
                        .ToDictionary(g => g.Key, g => g.ToList());

                    var maxDuplicateCount = groupedByCombinedId.Values.Any() 
                        ? groupedByCombinedId.Values.Max(g => g.Count) 
                        : 0;
                    var hasDuplicate = maxDuplicateCount > 1;

                    // 면접위원별 점수를 문항별로 합산
                    int? question1Sum = null;
                    int? question2Sum = null;
                    int? question3Sum = null;
                    int? question4Sum = null;
                    bool hasErrors = false;

                    foreach (var sheet in studentSheets)
                    {
                        // 오류가 있으면 표시
                        if (sheet.HasErrors)
                        {
                            hasErrors = true;
                        }

                        // 각 문항 점수 합산 (null이 아니면 합산)
                        if (sheet.Question1Marking.HasValue)
                        {
                            question1Sum = (question1Sum ?? 0) + sheet.Question1Marking.Value;
                        }
                        if (sheet.Question2Marking.HasValue)
                        {
                            question2Sum = (question2Sum ?? 0) + sheet.Question2Marking.Value;
                        }
                        if (sheet.Question3Marking.HasValue)
                        {
                            question3Sum = (question3Sum ?? 0) + sheet.Question3Marking.Value;
                        }
                        if (sheet.Question4Marking.HasValue)
                        {
                            question4Sum = (question4Sum ?? 0) + sheet.Question4Marking.Value;
                        }
                    }

                    // 수험번호당 하나의 GradingResult 생성
                    var result = new GradingResult
                    {
                        ImageId = studentSheets.First().ImageId, // 첫 번째 시트의 ImageId 사용
                        ImageFileName = string.Empty, // 파일명은 표시하지 않음
                        StudentId = studentId,
                        StudentName = studentInfo?.Name,
                        InterviewId = null, // 합산된 결과이므로 면접번호는 표시하지 않음
                        Question1Marking = question1Sum,
                        Question2Marking = question2Sum,
                        Question3Marking = question3Sum,
                        Question4Marking = question4Sum,
                        IsDuplicate = hasDuplicate,
                        DuplicateCount = maxDuplicateCount,
                        HasErrors = hasErrors,
                        // StudentInfo 필드들
                        RegistrationNumber = studentInfo?.RegistrationNumber,
                        MiddleSchool = studentInfo?.MiddleSchool
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
                    var errorResult = new GradingResult
                    {
                        ImageId = errorSheet.ImageId,
                        ImageFileName = string.Empty,
                        StudentId = null,
                        InterviewId = errorSheet.InterviewId,
                        Question1Marking = errorSheet.Question1Marking,
                        Question2Marking = errorSheet.Question2Marking,
                        Question3Marking = errorSheet.Question3Marking,
                        Question4Marking = errorSheet.Question4Marking,
                        HasErrors = errorSheet.HasErrors
                    };
                    CalculateScores(errorResult);
                    gradingResults.Add(errorResult);
                }

                // 6. 석차 계산 (TotalScore 기준)
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
            var scores = new System.Collections.Generic.List<int>();
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
