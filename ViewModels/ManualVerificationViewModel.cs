using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 수기 검산 화면용 ViewModel입니다. (샘플 20명 UI 유지)
    /// </summary>
    public class ManualVerificationViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly NavigationViewModel _navigation;
        private readonly OmrVerificationCore _core;
        private readonly OmrAnalysisCache _analysisCache = OmrAnalysisCache.Instance;
        private readonly GradingCalculator _gradingCalculator = GradingCalculator.Instance;
        private int _sampleSeed;
        private StudentSampleItem? _selectedStudent;

        public ManualVerificationViewModel(NavigationViewModel navigation, Workspace workspace)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _core = new OmrVerificationCore(navigation, workspace);
            _core.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

            NavigateToHomeCommand = new RelayCommand(() => _navigation.NavigateTo(ApplicationMode.Home));

            RegenerateSampleCommand = new RelayCommand(RegenerateSample, () => !IsBusy);
            ReloadCommand = new RelayCommand(() => _ = ReloadAsync(), () => !IsBusy);

            PreviousImageCommand = _core.PreviousImageCommand;
            NextImageCommand = _core.NextImageCommand;

            // 기본 seed: 매 진입마다 고정되지 않게
            _sampleSeed = Environment.TickCount;

            _ = ReloadAsync();
        }

        public NavigationViewModel Navigation => _navigation;

        public ICommand NavigateToHomeCommand { get; }
        public ICommand RegenerateSampleCommand { get; }
        public ICommand PreviousImageCommand { get; }
        public ICommand NextImageCommand { get; }
        public ICommand ReloadCommand { get; }

        public bool IsBusy => _core.IsBusy;
        public string? BusyMessage => _core.BusyMessage;

        public int SampleSeed
        {
            get => _sampleSeed;
            private set
            {
                if (_sampleSeed != value)
                {
                    _sampleSeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<StudentSampleItem> SampledStudents { get; } = new();

        public StudentSampleItem? SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (!ReferenceEquals(_selectedStudent, value))
                {
                    _selectedStudent = value;
                    OnPropertyChanged();

                    if (_selectedStudent == null)
                    {
                        _core.ClearStudent();
                    }
                    else
                    {
                        _core.SetStudent(_selectedStudent.StudentId);
                    }
                }
            }
        }

        // ---- 검산 공통 바인딩(Core 위임) ----
        public ObservableCollection<OmrSheetResult> StudentSheets => _core.StudentSheets;

        public OmrSheetResult? SelectedSheet
        {
            get => _core.SelectedSheet;
            set => _core.SelectedSheet = value;
        }

        public ImageDocument? SelectedDocument => _core.SelectedDocument;
        public System.Windows.Media.Imaging.BitmapSource? DisplayImage => _core.DisplayImage;
        public ObservableCollection<QuestionVerificationRow> QuestionRows => _core.QuestionRows;
        public GradingResult? SelectedStudentGradingResult => _core.SelectedStudentGradingResult;

        public Rect CurrentImageDisplayRect
        {
            get => _core.CurrentImageDisplayRect;
            set => _core.CurrentImageDisplayRect = value;
        }

        public double ZoomLevel
        {
            get => _core.ZoomLevel;
            set => _core.ZoomLevel = value;
        }

        public void UpdateImageDisplayRect(Size availableSize) => _core.UpdateImageDisplayRect(availableSize);

        private async Task ReloadAsync()
        {
            await _core.ReloadAsync();
            ApplySampling(SampleSeed);
        }

        private void RegenerateSample()
        {
            SampleSeed = Environment.TickCount;
            ApplySampling(SampleSeed);
        }

        private void ApplySampling(int seed)
        {
            SampledStudents.Clear();

            // NOTE: 전체 AnalyzeAllSheets를 강제하지 않도록 barcode-only 인덱스를 활용
            _ = Task.Run(async () =>
            {
                var sampledIds = await _gradingCalculator.GetRandomSampleStudentIdsAsync(20, seed);
                if (sampledIds.Count == 0)
                {
                    UiThread.Invoke(() => SelectedStudent = null);
                    return;
                }

                var idx = await _analysisCache.GetStudentIdIndexByImageIdAsync();
                var counts = idx.Values
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .GroupBy(id => id!, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

                var sampledSorted = sampledIds
                    .OrderBy(id => TryParseIntOrMax(id))
                    .ThenBy(id => id, StringComparer.Ordinal)
                    .ToList();

                UiThread.Invoke(() =>
                {
                    SampledStudents.Clear();
                    foreach (var studentId in sampledSorted)
                    {
                        counts.TryGetValue(studentId, out var imageCount);
                        SampledStudents.Add(new StudentSampleItem(studentId, imageCount));
                    }
                    SelectedStudent = SampledStudents.FirstOrDefault();
                });
            });
        }

        private static int TryParseIntOrMax(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return int.MaxValue;
            return int.TryParse(s.Trim(), out var n) ? n : int.MaxValue;
        }

        public void OnNavigatedTo(object? parameter)
        {
            // 샘플 검산 모드는 파라미터 없이 동작
        }

        public void OnNavigatedFrom()
        {
            _core.ReleaseHeavyResources();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public record StudentSampleItem(string StudentId, int ImageCount)
    {
        public override string ToString() => $"{StudentId} ({ImageCount}장)";
    }
}

