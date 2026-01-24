using System;
using System.IO;
using System.Linq;
using System.Windows;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Services.Validators;
using SimpleOverlayEditor.ViewModels;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly NavigationViewModel _navigation;
        private readonly StateStore _stateStore;
        private readonly Workspace _workspace;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // OMR 설정 검증 (애플리케이션 시작 시)
                var validationResult = OmrConfigurationValidator.ValidateConfiguration();
                if (!validationResult.IsValid)
                {
                    Logger.Instance.Error($"OMR 설정 검증 실패:\n{string.Join("\n", validationResult.Errors)}");
                    MessageBox.Show(
                        $"OMR 설정 검증 실패:\n\n{string.Join("\n", validationResult.Errors)}\n\n애플리케이션이 비정상적으로 동작할 수 있습니다.",
                        "설정 검증 오류",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    Logger.Instance.Info("OMR 설정 검증 통과");
                }

                // 회차 시스템 초기화
                var appStateStore = new AppStateStore();
                var appState = appStateStore.LoadAppState();

                // 회차 자동 발견 (재설치 시나리오 대응)
                var discoveredRounds = appStateStore.DiscoverRounds();
                foreach (var discovered in discoveredRounds)
                {
                    if (!appState.Rounds.Any(r => r.Name.Equals(discovered.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        appState.Rounds.Add(discovered);
                        Logger.Instance.Info($"회차 자동 발견: {discovered.Name}");
                    }
                }

                // 폴더가 없는 회차 정리 (수동 삭제 등으로 폴더만 삭제된 경우 대응)
                var validRounds = appState.Rounds
                    .Where(r => Directory.Exists(PathService.GetRoundRoot(r.Name)))
                    .ToList();

                if (validRounds.Count != appState.Rounds.Count)
                {
                    var removedCount = appState.Rounds.Count - validRounds.Count;
                    var removedNames = appState.Rounds
                        .Where(r => !Directory.Exists(PathService.GetRoundRoot(r.Name)))
                        .Select(r => r.Name)
                        .ToList();
                    
                    Logger.Instance.Info($"폴더가 없는 회차 {removedCount}개 정리: {string.Join(", ", removedNames)}");
                    
                    appState.Rounds = validRounds;
                    
                    // LastSelectedRound도 유효한 회차로 업데이트
                    if (!string.IsNullOrEmpty(appState.LastSelectedRound) && 
                        !validRounds.Any(r => r.Name.Equals(appState.LastSelectedRound, StringComparison.OrdinalIgnoreCase)))
                    {
                        appState.LastSelectedRound = null;
                    }
                    
                    appStateStore.SaveAppState(appState);
                    appState = appStateStore.LoadAppState(); // 다시 로드
                }

                // 기본 회차 생성 (2025년, 2026년이 없으면 항상 생성)
                var has2025 = Directory.Exists(PathService.GetRoundRoot("2025년")) ||
                              appState.Rounds.Any(r => r.Name.Equals("2025년", StringComparison.OrdinalIgnoreCase));
                var has2026 = Directory.Exists(PathService.GetRoundRoot("2026년")) ||
                              appState.Rounds.Any(r => r.Name.Equals("2026년", StringComparison.OrdinalIgnoreCase));

                if (!has2025 || !has2026)
                {
                    Logger.Instance.Info("기본 회차 생성: 2025년, 2026년");
                    
                    if (!has2025)
                    {
                        appStateStore.CreateRound("2025년");
                    }
                    
                    if (!has2026)
                    {
                        appStateStore.CreateRound("2026년");
                    }
                    
                    appStateStore.SaveAppState(appState);
                    appState = appStateStore.LoadAppState(); // 다시 로드
                }

                // 마지막 선택 회차 또는 기본 회차 선택
                var selectedRound = appState.LastSelectedRound;
                if (string.IsNullOrEmpty(selectedRound) || !appState.Rounds.Any(r => r.Name.Equals(selectedRound, StringComparison.OrdinalIgnoreCase)))
                {
                    // 가장 최근에 접근한 회차 선택 (LastAccessedAt 기준)
                    selectedRound = appState.Rounds
                        .OrderByDescending(r => r.LastAccessedAt)
                        .FirstOrDefault()?.Name ?? appState.Rounds.FirstOrDefault()?.Name ?? "2025년";
                }

                // PathService.CurrentRound 설정
                PathService.CurrentRound = selectedRound;
                Logger.Instance.Info($"회차 선택: {selectedRound}");

                // 회차 접근 시각 업데이트
                appStateStore.UpdateRoundAccessTime(selectedRound);

                // 서비스 초기화
                _stateStore = new StateStore();

                // Workspace 로드 (현재 회차 기준)
                _workspace = _stateStore.Load();
                if (string.IsNullOrEmpty(_workspace.InputFolderPath))
                {
                    _workspace.InputFolderPath = PathService.DefaultInputFolder;
                }

                // NavigationViewModel 생성
                _navigation = new NavigationViewModel();

                // NOTE:
                // ViewModel 생성 로직은 MainNavigationViewModel에서 단일화하여 처리합니다.
                // (Navigation이 ViewModel을 캐시/재사용할 수 있도록 중복 생성 경로 제거)

                // 초기 모드: 홈 화면 (DataContext 설정 전에 ViewModel 생성)
                Logger.Instance.Info($"NavigateTo 호출 전. CurrentMode: {_navigation.CurrentMode}, CurrentViewModel: {(_navigation.CurrentViewModel != null ? _navigation.CurrentViewModel.GetType().Name : "null")}");
                _navigation.NavigateTo(ApplicationMode.Home);
                Logger.Instance.Info($"NavigateTo 호출 후. CurrentMode: {_navigation.CurrentMode}, CurrentViewModel: {(_navigation.CurrentViewModel != null ? _navigation.CurrentViewModel.GetType().Name : "null")}");
                
                // 초기화 시에는 PropertyChanged가 발생하지 않을 수 있으므로 직접 ViewModel 생성
                Logger.Instance.Info($"강제 생성 체크 시작. CurrentViewModel == null: {_navigation.CurrentViewModel == null}");
                if (_navigation.CurrentViewModel == null)
                {
                    Logger.Instance.Info($"HomeViewModel 생성 시작");
                    var homeViewModel = new HomeViewModel(_navigation, _workspace, _stateStore);
                    Logger.Instance.Info($"HomeViewModel 생성 완료. 타입: {homeViewModel.GetType().Name}");
                    Logger.Instance.Info($"SetHomeViewModel 호출 전. CurrentMode: {_navigation.CurrentMode}");
                    _navigation.SetHomeViewModel(homeViewModel);
                    Logger.Instance.Info($"SetHomeViewModel 호출 후. CurrentViewModel: {(_navigation.CurrentViewModel != null ? _navigation.CurrentViewModel.GetType().Name : "null")}");
                    Logger.Instance.Info($"초기화 시 HomeViewModel 강제 생성 완료. CurrentViewModel: {(_navigation.CurrentViewModel != null ? _navigation.CurrentViewModel.GetType().Name : "null")}");
                }
                else
                {
                    Logger.Instance.Info($"강제 생성 스킵: CurrentViewModel이 이미 존재함. 타입: {_navigation.CurrentViewModel.GetType().Name}");
                }

                // DataContext 설정 (NavigationViewModel을 포함하는 래퍼)
                DataContext = new MainNavigationViewModel(_navigation, _workspace, _stateStore);

                Logger.Instance.Info($"MainWindow 초기화 완료. CurrentViewModel: {(_navigation.CurrentViewModel != null ? _navigation.CurrentViewModel.GetType().Name : "null")}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("MainWindow 초기화 실패", ex);
                MessageBox.Show(
                    $"창 초기화 중 오류가 발생했습니다:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "초기화 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 종료 시 작업 상황만 자동 저장 (템플릿은 저장하지 않음)
            try
            {
                _stateStore.SaveWorkspaceState(_workspace);
                Logger.Instance.Info("MainWindow 종료 - 작업 상황 저장 완료");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("MainWindow 종료 - 작업 상황 저장 실패", ex);
            }

            base.OnClosed(e);
        }
    }

    /// <summary>
    /// Navigation과 Workspace를 함께 관리하는 ViewModel
    /// </summary>
    public class MainNavigationViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private readonly NavigationViewModel _navigation;
        private readonly Workspace _workspace;
        private readonly StateStore _stateStore;

        public MainNavigationViewModel(NavigationViewModel navigation, Workspace workspace, StateStore stateStore)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));

            // Navigation의 모든 속성 변경 감지 (특히 CurrentViewModel)
            _navigation.PropertyChanged += (s, e) =>
            {
                Services.Logger.Instance.Debug($"MainNavigationViewModel: Navigation.PropertyChanged 발생. PropertyName: {e.PropertyName}");
                
                // ViewModel 생성 로직 (CurrentMode 변경 또는 CurrentViewModel이 null로 변경될 때)
                if (e.PropertyName == nameof(Navigation.CurrentMode) || 
                    (e.PropertyName == nameof(Navigation.CurrentViewModel) && _navigation.CurrentViewModel == null))
                {
                    Services.Logger.Instance.Info($"MainNavigationViewModel: ViewModel 생성 체크. CurrentMode: {_navigation.CurrentMode}, CurrentViewModel: {(_navigation.CurrentViewModel?.GetType().Name ?? "null")}");
                    
                    // CurrentViewModel이 null이면 생성
                    if (_navigation.CurrentViewModel == null)
                    {
                        if (_navigation.CurrentMode == ApplicationMode.Home)
                        {
                            Services.Logger.Instance.Info($"MainNavigationViewModel: HomeViewModel 생성 시작");
                            var homeViewModel = new HomeViewModel(_navigation, _workspace, _stateStore);
                            _navigation.SetHomeViewModel(homeViewModel);
                            Services.Logger.Instance.Info($"MainNavigationViewModel: HomeViewModel 생성 완료");
                        }
                        else if (_navigation.CurrentMode == ApplicationMode.TemplateEdit)
                        {
                            Services.Logger.Instance.Info($"MainNavigationViewModel: TemplateEditViewModel 생성 시작");
                            var templateEditViewModel = new TemplateEditViewModel(_navigation, _workspace, _stateStore);
                            _navigation.SetTemplateEditViewModel(templateEditViewModel);
                            Services.Logger.Instance.Info($"MainNavigationViewModel: TemplateEditViewModel 생성 완료");
                        }
                        else if (_navigation.CurrentMode == ApplicationMode.Marking)
                        {
                            Services.Logger.Instance.Info($"MainNavigationViewModel: MarkingViewModel 생성 시작");
                            var markingDetector = new Services.MarkingDetector();
                            var markingViewModel = new MarkingViewModel(markingDetector, _navigation, _workspace, _stateStore);
                            
                            // ScoringAreas만 설정 (템플릿은 공유)
                            markingViewModel.ScoringAreas = _workspace.Template.ScoringAreas;
                            
                            // Documents와 SelectedDocument는 null로 시작 (사용자가 폴더를 로드해야 함)
                            
                            _navigation.SetMarkingViewModel(markingViewModel);
                            Services.Logger.Instance.Info($"MainNavigationViewModel: MarkingViewModel 생성 완료");
                        }
                        else if (_navigation.CurrentMode == ApplicationMode.Registry)
                        {
                            Services.Logger.Instance.Info($"MainNavigationViewModel: RegistryViewModel 생성 시작");
                            var registryViewModel = new RegistryViewModel(_navigation);
                            _navigation.SetRegistryViewModel(registryViewModel);
                            Services.Logger.Instance.Info($"MainNavigationViewModel: RegistryViewModel 생성 완료");
                        }
                        else if (_navigation.CurrentMode == ApplicationMode.Grading)
                        {
                            Services.Logger.Instance.Info($"MainNavigationViewModel: GradingViewModel 생성 시작");
                            var gradingViewModel = new GradingViewModel(_navigation);
                            _navigation.SetGradingViewModel(gradingViewModel);
                            Services.Logger.Instance.Info($"MainNavigationViewModel: GradingViewModel 생성 완료");
                        }
                        else if (_navigation.CurrentMode == ApplicationMode.ScoringRule)
                        {
                            Services.Logger.Instance.Info($"MainNavigationViewModel: ScoringRuleViewModel 생성 시작");
                            var scoringRuleViewModel = new ScoringRuleViewModel(_navigation);
                            _navigation.SetScoringRuleViewModel(scoringRuleViewModel);
                            Services.Logger.Instance.Info($"MainNavigationViewModel: ScoringRuleViewModel 생성 완료");
                        }
                        else if (_navigation.CurrentMode == ApplicationMode.ManualVerification)
                        {
                            Services.Logger.Instance.Info($"MainNavigationViewModel: ManualVerificationViewModel 생성 시작");
                            var manualVerificationViewModel = new ManualVerificationViewModel(_navigation, _workspace);
                            _navigation.SetManualVerificationViewModel(manualVerificationViewModel);
                            Services.Logger.Instance.Info($"MainNavigationViewModel: ManualVerificationViewModel 생성 완료");
                        }
                        else if (_navigation.CurrentMode == ApplicationMode.SingleStudentVerification)
                        {
                            Services.Logger.Instance.Info($"MainNavigationViewModel: SingleStudentVerificationViewModel 생성 시작");
                            var singleStudentVerificationViewModel = new SingleStudentVerificationViewModel(_navigation, _workspace);
                            _navigation.SetSingleStudentVerificationViewModel(singleStudentVerificationViewModel);
                            Services.Logger.Instance.Info($"MainNavigationViewModel: SingleStudentVerificationViewModel 생성 완료");
                        }
                    }
                }
                
                if (e.PropertyName == nameof(Navigation.CurrentMode) || e.PropertyName == nameof(Navigation.CurrentViewModel))
                {
                    Services.Logger.Instance.Debug($"MainNavigationViewModel: CurrentViewModel PropertyChanged 발생");
                    OnPropertyChanged(nameof(Navigation));
                    OnPropertyChanged(nameof(CurrentViewModel)); // 직접 노출하는 프로퍼티도 업데이트
                }
            };
        }

        public NavigationViewModel Navigation => _navigation;
        public Workspace Workspace => _workspace;
        
        /// <summary>
        /// 현재 ViewModel을 직접 노출 (바인딩 편의성)
        /// </summary>
        public object? CurrentViewModel => _navigation.CurrentViewModel;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
