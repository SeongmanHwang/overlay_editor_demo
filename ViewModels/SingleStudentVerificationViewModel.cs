using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 성적처리에서 선택한 '단일 학생'만 검산하는 전용 ViewModel입니다.
    /// </summary>
    public class SingleStudentVerificationViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly NavigationViewModel _navigation;
        private readonly OmrVerificationCore _core;
        private string? _studentId;
        private string? _statusMessage;

        public SingleStudentVerificationViewModel(NavigationViewModel navigation, Workspace workspace)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _core = new OmrVerificationCore(navigation, workspace);
            _core.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);

            NavigateToHomeCommand = new RelayCommand(() => _navigation.NavigateTo(ApplicationMode.Home));
            ReloadCommand = new RelayCommand(() => _ = ReloadAsync(), () => !IsBusy);
        }

        public NavigationViewModel Navigation => _navigation;

        public ICommand NavigateToHomeCommand { get; }
        public ICommand ReloadCommand { get; }

        public bool IsBusy => _core.IsBusy;
        public string? BusyMessage => _core.BusyMessage;

        /// <summary>
        /// 현재 대상 수험번호
        /// </summary>
        public string? StudentId
        {
            get => _studentId;
            set
            {
                if (_studentId != value)
                {
                    _studentId = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
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

        public ICommand PreviousImageCommand => _core.PreviousImageCommand;
        public ICommand NextImageCommand => _core.NextImageCommand;

        public void UpdateImageDisplayRect(Size availableSize) => _core.UpdateImageDisplayRect(availableSize);

        public void OnNavigatedTo(object? parameter)
        {
            var id = (parameter as string)?.Trim();
            StudentId = string.IsNullOrWhiteSpace(id) ? null : id;
            StatusMessage = null;

            _ = NavigateToStudentAsync(StudentId);
        }

        public void OnNavigatedFrom()
        {
            _core.ReleaseHeavyResources();
        }

        private async Task NavigateToStudentAsync(string? studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                _core.ClearStudent();
                StatusMessage = "수험번호가 전달되지 않았습니다.";
                return;
            }

            await _core.EnsureLoadedForStudentAsync(studentId);

            var ok = _core.SetStudent(studentId);
            StatusMessage = ok ? null : $"해당 수험번호의 OMR 결과가 없습니다: {studentId}";
        }

        private async Task ReloadAsync()
        {
            await _core.ReloadAsync();
            await NavigateToStudentAsync(StudentId);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

