using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 애플리케이션의 네비게이션을 관리하는 ViewModel입니다.
    /// </summary>
    public class NavigationViewModel : INotifyPropertyChanged
    {
        private ApplicationMode _currentMode = ApplicationMode.Home;
        private object? _currentViewModel;

        public NavigationViewModel()
        {
            NavigateToHomeCommand = new RelayCommand(() => NavigateTo(ApplicationMode.Home));
            NavigateToTemplateEditCommand = new RelayCommand(() => NavigateTo(ApplicationMode.TemplateEdit));
            NavigateToMarkingCommand = new RelayCommand(() => NavigateTo(ApplicationMode.Marking));
            NavigateToRegistryCommand = new RelayCommand(() => NavigateTo(ApplicationMode.Registry));
            NavigateToGradingCommand = new RelayCommand(() => NavigateTo(ApplicationMode.Grading));
            NavigateToScoringRuleCommand = new RelayCommand(() => NavigateTo(ApplicationMode.ScoringRule));
        }

        /// <summary>
        /// 현재 모드
        /// </summary>
        public ApplicationMode CurrentMode
        {
            get => _currentMode;
            private set
            {
                if (_currentMode != value)
                {
                    _currentMode = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 현재 ViewModel (모드에 따라 변경됨)
        /// </summary>
        public object? CurrentViewModel
        {
            get => _currentViewModel;
            private set
            {
                if (_currentViewModel != value)
                {
                    Services.Logger.Instance.Debug($"CurrentViewModel 변경: {_currentViewModel?.GetType().Name ?? "null"} → {value?.GetType().Name ?? "null"}");
                    _currentViewModel = value;
                    OnPropertyChanged();
                    Services.Logger.Instance.Debug($"CurrentViewModel PropertyChanged 이벤트 발생 완료");
                }
            }
        }

        /// <summary>
        /// 홈 화면으로 이동
        /// </summary>
        public ICommand NavigateToHomeCommand { get; }

        /// <summary>
        /// 템플릿 편집 모드로 이동
        /// </summary>
        public ICommand NavigateToTemplateEditCommand { get; }

        /// <summary>
        /// 마킹 리딩 모드로 이동
        /// </summary>
        public ICommand NavigateToMarkingCommand { get; }

        /// <summary>
        /// 수험생 명렬 관리 모드로 이동
        /// </summary>
        public ICommand NavigateToRegistryCommand { get; }

        /// <summary>
        /// 채점 및 성적 처리 모드로 이동
        /// </summary>
        public ICommand NavigateToGradingCommand { get; }

        /// <summary>
        /// 정답 및 배점 모드로 이동
        /// </summary>
        public ICommand NavigateToScoringRuleCommand { get; }

        /// <summary>
        /// 특정 모드로 이동합니다.
        /// </summary>
        public void NavigateTo(ApplicationMode mode)
        {
            Services.Logger.Instance.Info($"모드 전환: {CurrentMode} → {mode}");

            CurrentMode = mode;

            // 모드에 따라 ViewModel 생성/설정
            // 순환 참조를 피하기 위해 HomeViewModel은 외부에서 설정
            if (mode == ApplicationMode.Home)
            {
                // HomeViewModel은 외부에서 생성하여 설정하도록 함
                // MainWindow에서 PropertyChanged 이벤트를 통해 ViewModel 생성
                CurrentViewModel = null; // 임시로 null 설정, MainWindow에서 설정됨
            }
            else if (mode == ApplicationMode.TemplateEdit)
            {
                // TemplateEdit 모드는 외부에서 ViewModel을 설정하도록 함
                // MainWindow에서 PropertyChanged 이벤트를 통해 ViewModel 생성
                CurrentViewModel = null; // 임시로 null 설정, MainWindow에서 설정됨
            }
            else if (mode == ApplicationMode.Marking)
            {
                // Marking 모드는 외부에서 ViewModel을 설정하도록 함
                // MainWindow에서 PropertyChanged 이벤트를 통해 ViewModel 생성
                CurrentViewModel = null; // 임시로 null 설정, MainWindow에서 설정됨
            }
            else if (mode == ApplicationMode.Registry)
            {
                // Registry 모드는 외부에서 ViewModel을 설정하도록 함
                // MainWindow에서 PropertyChanged 이벤트를 통해 ViewModel 생성
                CurrentViewModel = null; // 임시로 null 설정, MainWindow에서 설정됨
            }
            else if (mode == ApplicationMode.Grading)
            {
                // Grading 모드는 외부에서 ViewModel을 설정하도록 함
                // MainWindow에서 PropertyChanged 이벤트를 통해 ViewModel 생성
                CurrentViewModel = null; // 임시로 null 설정, MainWindow에서 설정됨
            }
            else if (mode == ApplicationMode.ScoringRule)
            {
                // ScoringRule 모드는 외부에서 ViewModel을 설정하도록 함
                // MainWindow에서 PropertyChanged 이벤트를 통해 ViewModel 생성
                CurrentViewModel = null; // 임시로 null 설정, MainWindow에서 설정됨
            }
            else
            {
                throw new ArgumentException($"알 수 없는 모드: {mode}", nameof(mode));
            }

            Services.Logger.Instance.Info($"모드 전환 완료. CurrentViewModel 타입: {CurrentViewModel?.GetType().Name ?? "null"}");
        }

        /// <summary>
        /// 외부에서 HomeViewModel을 설정할 수 있도록 합니다.
        /// </summary>
        public void SetHomeViewModel(object viewModel)
        {
            Services.Logger.Instance.Info($"SetHomeViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel?.GetType().Name}");
            if (CurrentMode == ApplicationMode.Home)
            {
                CurrentViewModel = viewModel;
                Services.Logger.Instance.Info($"SetHomeViewModel 완료. CurrentViewModel: {(CurrentViewModel != null ? CurrentViewModel.GetType().Name : "null")}");
            }
            else
            {
                Services.Logger.Instance.Warning($"SetHomeViewModel 실패: CurrentMode가 Home이 아님 (CurrentMode: {CurrentMode})");
            }
        }

        /// <summary>
        /// 외부에서 TemplateEditViewModel을 설정할 수 있도록 합니다.
        /// </summary>
        public void SetTemplateEditViewModel(object viewModel)
        {
            Services.Logger.Instance.Info($"SetTemplateEditViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel?.GetType().Name}");
            if (CurrentMode == ApplicationMode.TemplateEdit)
            {
                CurrentViewModel = viewModel;
                Services.Logger.Instance.Info($"SetTemplateEditViewModel 완료. CurrentViewModel: {(CurrentViewModel != null ? CurrentViewModel.GetType().Name : "null")}");
            }
            else
            {
                Services.Logger.Instance.Warning($"SetTemplateEditViewModel 실패: CurrentMode가 TemplateEdit이 아님 (CurrentMode: {CurrentMode})");
            }
        }

        /// <summary>
        /// 외부에서 MarkingViewModel을 설정할 수 있도록 합니다.
        /// </summary>
        public void SetMarkingViewModel(object viewModel)
        {
            Services.Logger.Instance.Info($"SetMarkingViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel?.GetType().Name}");
            if (CurrentMode == ApplicationMode.Marking)
            {
                CurrentViewModel = viewModel;
                Services.Logger.Instance.Info($"SetMarkingViewModel 완료. CurrentViewModel: {(CurrentViewModel != null ? CurrentViewModel.GetType().Name : "null")}");
            }
            else
            {
                Services.Logger.Instance.Warning($"SetMarkingViewModel 실패: CurrentMode가 Marking이 아님 (CurrentMode: {CurrentMode})");
            }
        }

        /// <summary>
        /// 외부에서 RegistryViewModel을 설정할 수 있도록 합니다.
        /// </summary>
        public void SetRegistryViewModel(object viewModel)
        {
            Services.Logger.Instance.Info($"SetRegistryViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel?.GetType().Name}");
            if (CurrentMode == ApplicationMode.Registry)
            {
                CurrentViewModel = viewModel;
                Services.Logger.Instance.Info($"SetRegistryViewModel 완료. CurrentViewModel: {(CurrentViewModel != null ? CurrentViewModel.GetType().Name : "null")}");
            }
            else
            {
                Services.Logger.Instance.Warning($"SetRegistryViewModel 실패: CurrentMode가 Registry가 아님 (CurrentMode: {CurrentMode})");
            }
        }

        /// <summary>
        /// 외부에서 GradingViewModel을 설정할 수 있도록 합니다.
        /// </summary>
        public void SetGradingViewModel(object viewModel)
        {
            Services.Logger.Instance.Info($"SetGradingViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel?.GetType().Name}");
            if (CurrentMode == ApplicationMode.Grading)
            {
                CurrentViewModel = viewModel;
                Services.Logger.Instance.Info($"SetGradingViewModel 완료. CurrentViewModel: {(CurrentViewModel != null ? CurrentViewModel.GetType().Name : "null")}");
            }
            else
            {
                Services.Logger.Instance.Warning($"SetGradingViewModel 실패: CurrentMode가 Grading이 아님 (CurrentMode: {CurrentMode})");
            }
        }

        /// <summary>
        /// 외부에서 ScoringRuleViewModel을 설정할 수 있도록 합니다.
        /// </summary>
        public void SetScoringRuleViewModel(object viewModel)
        {
            Services.Logger.Instance.Info($"SetScoringRuleViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel?.GetType().Name}");
            if (CurrentMode == ApplicationMode.ScoringRule)
            {
                CurrentViewModel = viewModel;
                Services.Logger.Instance.Info($"SetScoringRuleViewModel 완료. CurrentViewModel: {(CurrentViewModel != null ? CurrentViewModel.GetType().Name : "null")}");
            }
            else
            {
                Services.Logger.Instance.Warning($"SetScoringRuleViewModel 실패: CurrentMode가 ScoringRule이 아님 (CurrentMode: {CurrentMode})");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

