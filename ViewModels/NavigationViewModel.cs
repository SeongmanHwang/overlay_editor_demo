using System;
using System.Collections.Generic;
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
        private readonly Stack<(ApplicationMode Mode, object? Parameter)> _history = new();
        private readonly Dictionary<ApplicationMode, object> _cache = new();
        private readonly Dictionary<ApplicationMode, object?> _lastParameters = new();
        private ApplicationMode? _pendingMode;
        private object? _pendingParameter;

        public NavigationViewModel()
        {
            NavigateToHomeCommand = new RelayCommand(() => NavigateTo(ApplicationMode.Home));
            NavigateToTemplateEditCommand = new RelayCommand(() => NavigateTo(ApplicationMode.TemplateEdit));
            NavigateToMarkingCommand = new RelayCommand(() => NavigateTo(ApplicationMode.Marking));
            NavigateToRegistryCommand = new RelayCommand(() => NavigateTo(ApplicationMode.Registry));
            NavigateToGradingCommand = new RelayCommand(() => NavigateTo(ApplicationMode.Grading));
            NavigateToScoringRuleCommand = new RelayCommand(() => NavigateTo(ApplicationMode.ScoringRule));
            NavigateToManualVerificationCommand = new RelayCommand(() => NavigateTo(ApplicationMode.ManualVerification));
            GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);
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
        /// 수기 검산 모드로 이동
        /// </summary>
        public ICommand NavigateToManualVerificationCommand { get; }

        /// <summary>
        /// 뒤로가기 가능 여부
        /// </summary>
        public bool CanGoBack => _history.Count > 0;

        /// <summary>
        /// 뒤로가기
        /// </summary>
        public ICommand GoBackCommand { get; }

        /// <summary>
        /// 히스토리 기록이 필요한 전환인지 판단합니다.
        /// 뒤로가기가 필요한 상세 화면으로 진입할 때만 히스토리를 기록합니다.
        /// </summary>
        /// <param name="from">현재 모드</param>
        /// <param name="to">전환할 모드</param>
        /// <returns>히스토리에 기록해야 하면 true, 아니면 false</returns>
        private static bool ShouldAddToHistory(ApplicationMode from, ApplicationMode to)
        {
            // SingleStudentVerification으로 진입할 때만 히스토리 기록
            // (다른 상세 화면이 추가되면 여기에 추가)
            return to == ApplicationMode.SingleStudentVerification;
        }

        /// <summary>
        /// 특정 모드로 이동합니다.
        /// 히스토리 기록 여부는 ShouldAddToHistory 정책에 따라 자동 결정됩니다.
        /// </summary>
        /// <param name="mode">전환할 모드</param>
        public void NavigateTo(ApplicationMode mode)
            => NavigateTo(mode, parameter: null);

        /// <summary>
        /// 특정 모드로 이동합니다. (필요 시 파라미터 전달)
        /// 히스토리 기록 여부는 ShouldAddToHistory 정책에 따라 자동 결정됩니다.
        /// </summary>
        /// <param name="mode">전환할 모드</param>
        /// <param name="parameter">모드에 전달할 파라미터 (예: SingleStudentVerification의 경우 studentId)</param>
        public void NavigateTo(ApplicationMode mode, object? parameter)
            => NavigateTo(mode, parameter, ShouldAddToHistory(CurrentMode, mode));

        /// <summary>
        /// 특정 모드로 이동합니다. (필요 시 파라미터 전달, 히스토리 기록 여부 명시적 제어)
        /// </summary>
        public void NavigateTo(ApplicationMode mode, object? parameter, bool addToHistory)
        {
            Services.Logger.Instance.Info($"모드 전환: {CurrentMode} → {mode}");

            // Home 진입 시 히스토리 리셋
            if (mode == ApplicationMode.Home && addToHistory)
            {
                _history.Clear();
                OnPropertyChanged(nameof(CanGoBack));
                if (GoBackCommand is RelayCommand goBack) goBack.RaiseCanExecuteChanged();
                addToHistory = false; // Home으로는 히스토리 기록하지 않음
            }

            if (mode == CurrentMode)
            {
                // 같은 화면 내에서 파라미터만 갱신하는 경우
                if (CurrentViewModel is INavigationAware sameVm)
                {
                    sameVm.OnNavigatedTo(parameter);
                }
                // 파라미터 추적 업데이트
                _lastParameters[mode] = parameter;
                return;
            }

            // 현재 화면 이탈 콜백
            if (CurrentViewModel is INavigationAware leavingVm)
            {
                leavingVm.OnNavigatedFrom();
            }

            if (addToHistory)
            {
                // 현재 모드와 마지막 파라미터를 히스토리에 저장
                // 히스토리에 저장된 파라미터는 GoBack() 시 복원됩니다.
                var lastParameter = _lastParameters.GetValueOrDefault(CurrentMode);
                _history.Push((CurrentMode, lastParameter));
                OnPropertyChanged(nameof(CanGoBack));
                if (GoBackCommand is RelayCommand goBack) goBack.RaiseCanExecuteChanged();
            }

            CurrentMode = mode;
            
            // 파라미터 추적
            _lastParameters[mode] = parameter;

            // 캐시된 ViewModel이 있으면 재사용 (상태 유지)
            if (_cache.TryGetValue(mode, out var cachedVm))
            {
                _pendingMode = null;
                _pendingParameter = null;
                CurrentViewModel = cachedVm;

                if (cachedVm is INavigationAware enteringVm)
                {
                    enteringVm.OnNavigatedTo(parameter);
                }
            }
            else
            {
                // 아직 생성되지 않은 모드: MainWindow에서 생성하도록 트리거 (CurrentViewModel=null)
                _pendingMode = mode;
                _pendingParameter = parameter;
                CurrentViewModel = null;
            }

            Services.Logger.Instance.Info($"모드 전환 완료. CurrentViewModel 타입: {CurrentViewModel?.GetType().Name ?? "null"}");
        }

        private void ApplyPendingNavigationIfMatched(ApplicationMode mode, object viewModel)
        {
            if (_pendingMode != mode) return;

            var param = _pendingParameter;
            _pendingMode = null;
            _pendingParameter = null;

            if (viewModel is INavigationAware nav)
            {
                nav.OnNavigatedTo(param);
            }
        }

        private void CacheAndSet(ApplicationMode mode, object viewModel)
        {
            _cache[mode] = viewModel;

            if (CurrentMode == mode)
            {
                CurrentViewModel = viewModel;
            }

            ApplyPendingNavigationIfMatched(mode, viewModel);
        }

        /// <summary>
        /// 히스토리를 사용하여 이전 화면으로 돌아갑니다.
        /// 히스토리에 저장된 모드와 파라미터를 함께 복원합니다.
        /// </summary>
        public void GoBack()
        {
            if (_history.Count == 0) return;

            // 히스토리에서 이전 모드와 파라미터를 함께 꺼내서 복원
            var (previousMode, previousParameter) = _history.Pop();
            OnPropertyChanged(nameof(CanGoBack));
            if (GoBackCommand is RelayCommand goBack) goBack.RaiseCanExecuteChanged();

            // 뒤로가기 시에는 히스토리에 다시 기록하지 않음 (addToHistory: false)
            NavigateTo(previousMode, previousParameter, addToHistory: false);
        }

        /// <summary>
        /// 외부에서 HomeViewModel을 설정할 수 있도록 합니다.
        /// </summary>
        public void SetHomeViewModel(object viewModel)
        {
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));
            Services.Logger.Instance.Info($"SetHomeViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel.GetType().Name}");
            if (CurrentMode == ApplicationMode.Home)
            {
                CacheAndSet(ApplicationMode.Home, viewModel);
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
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));
            Services.Logger.Instance.Info($"SetTemplateEditViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel.GetType().Name}");
            if (CurrentMode == ApplicationMode.TemplateEdit)
            {
                CacheAndSet(ApplicationMode.TemplateEdit, viewModel);
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
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));
            Services.Logger.Instance.Info($"SetMarkingViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel.GetType().Name}");
            if (CurrentMode == ApplicationMode.Marking)
            {
                CacheAndSet(ApplicationMode.Marking, viewModel);
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
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));
            Services.Logger.Instance.Info($"SetRegistryViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel.GetType().Name}");
            if (CurrentMode == ApplicationMode.Registry)
            {
                CacheAndSet(ApplicationMode.Registry, viewModel);
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
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));
            Services.Logger.Instance.Info($"SetGradingViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel.GetType().Name}");
            if (CurrentMode == ApplicationMode.Grading)
            {
                CacheAndSet(ApplicationMode.Grading, viewModel);
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
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));
            Services.Logger.Instance.Info($"SetScoringRuleViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel.GetType().Name}");
            if (CurrentMode == ApplicationMode.ScoringRule)
            {
                CacheAndSet(ApplicationMode.ScoringRule, viewModel);
                Services.Logger.Instance.Info($"SetScoringRuleViewModel 완료. CurrentViewModel: {(CurrentViewModel != null ? CurrentViewModel.GetType().Name : "null")}");
            }
            else
            {
                Services.Logger.Instance.Warning($"SetScoringRuleViewModel 실패: CurrentMode가 ScoringRule이 아님 (CurrentMode: {CurrentMode})");
            }
        }

        /// <summary>
        /// 외부에서 ManualVerificationViewModel을 설정할 수 있도록 합니다.
        /// </summary>
        public void SetManualVerificationViewModel(object viewModel)
        {
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));
            Services.Logger.Instance.Info($"SetManualVerificationViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel.GetType().Name}");
            if (CurrentMode == ApplicationMode.ManualVerification)
            {
                CacheAndSet(ApplicationMode.ManualVerification, viewModel);
                Services.Logger.Instance.Info($"SetManualVerificationViewModel 완료. CurrentViewModel: {(CurrentViewModel != null ? CurrentViewModel.GetType().Name : "null")}");
            }
            else
            {
                Services.Logger.Instance.Warning($"SetManualVerificationViewModel 실패: CurrentMode가 ManualVerification이 아님 (CurrentMode: {CurrentMode})");
            }
        }

        /// <summary>
        /// 외부에서 SingleStudentVerificationViewModel을 설정할 수 있도록 합니다.
        /// </summary>
        public void SetSingleStudentVerificationViewModel(object viewModel)
        {
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));
            Services.Logger.Instance.Info($"SetSingleStudentVerificationViewModel 호출. CurrentMode: {CurrentMode}, ViewModel 타입: {viewModel.GetType().Name}");
            if (CurrentMode == ApplicationMode.SingleStudentVerification)
            {
                CacheAndSet(ApplicationMode.SingleStudentVerification, viewModel);
                Services.Logger.Instance.Info($"SetSingleStudentVerificationViewModel 완료. CurrentViewModel: {(CurrentViewModel != null ? CurrentViewModel.GetType().Name : "null")}");
            }
            else
            {
                Services.Logger.Instance.Warning($"SetSingleStudentVerificationViewModel 실패: CurrentMode가 SingleStudentVerification이 아님 (CurrentMode: {CurrentMode})");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

