using System;
using System.Linq;
using System.Windows;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
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

                // 서비스 초기화
                _stateStore = new StateStore();

                // Workspace 로드
                _workspace = _stateStore.Load();
                if (string.IsNullOrEmpty(_workspace.InputFolderPath))
                {
                    _workspace.InputFolderPath = PathService.DefaultInputFolder;
                }

                // NavigationViewModel 생성
                _navigation = new NavigationViewModel();

            // Navigation의 모드 변경 감지 (ViewModel 생성)
            _navigation.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_navigation.CurrentMode))
                {
                    if (_navigation.CurrentMode == ApplicationMode.Home && _navigation.CurrentViewModel == null)
                    {
                        // Home 모드로 전환 시 ViewModel 생성
                        var homeViewModel = new HomeViewModel(_navigation);
                        _navigation.SetHomeViewModel(homeViewModel);
                    }
                    else if (_navigation.CurrentMode == ApplicationMode.TemplateEdit && _navigation.CurrentViewModel == null)
                    {
                        // TemplateEdit 모드로 전환 시 ViewModel 생성
                        var templateEditViewModel = new TemplateEditViewModel(_navigation, _workspace, _stateStore);
                        _navigation.SetTemplateEditViewModel(templateEditViewModel);
                    }
                    else if (_navigation.CurrentMode == ApplicationMode.Marking && _navigation.CurrentViewModel == null)
                    {
                        // Marking 모드로 전환 시 ViewModel 생성
                        // 완전히 빈 상태로 시작 (이전 세션 데이터 로드 안 함)
                        var markingDetector = new Services.MarkingDetector();
                        var markingViewModel = new MarkingViewModel(markingDetector, _navigation, _workspace, _stateStore);
                        
                        // ScoringAreas만 설정 (템플릿은 공유)
                        markingViewModel.ScoringAreas = _workspace.Template.ScoringAreas;
                        
                        // Documents와 SelectedDocument는 null로 시작 (사용자가 폴더를 로드해야 함)
                        
                        _navigation.SetMarkingViewModel(markingViewModel);
                    }
                    else if (_navigation.CurrentMode == ApplicationMode.Registry && _navigation.CurrentViewModel == null)
                    {
                        // Registry 모드로 전환 시 ViewModel 생성
                        var registryViewModel = new RegistryViewModel(_navigation);
                        _navigation.SetRegistryViewModel(registryViewModel);
                    }
                    else if (_navigation.CurrentMode == ApplicationMode.Grading && _navigation.CurrentViewModel == null)
                    {
                        // Grading 모드로 전환 시 ViewModel 생성
                        var gradingViewModel = new GradingViewModel(_navigation);
                        _navigation.SetGradingViewModel(gradingViewModel);
                    }
                    else if (_navigation.CurrentMode == ApplicationMode.ScoringRule && _navigation.CurrentViewModel == null)
                    {
                        // ScoringRule 모드로 전환 시 ViewModel 생성
                        var scoringRuleViewModel = new ScoringRuleViewModel(_navigation);
                        _navigation.SetScoringRuleViewModel(scoringRuleViewModel);
                    }
                    else if (_navigation.CurrentMode == ApplicationMode.ManualVerification && _navigation.CurrentViewModel == null)
                    {
                        // ManualVerification 모드로 전환 시 ViewModel 생성
                        var manualVerificationViewModel = new ManualVerificationViewModel(_navigation);
                        _navigation.SetManualVerificationViewModel(manualVerificationViewModel);
                    }
                }
            };

                // 초기 모드: 홈 화면 (DataContext 설정 전에 ViewModel 생성)
                Logger.Instance.Info($"NavigateTo 호출 전. CurrentMode: {_navigation.CurrentMode}, CurrentViewModel: {(_navigation.CurrentViewModel != null ? _navigation.CurrentViewModel.GetType().Name : "null")}");
                _navigation.NavigateTo(ApplicationMode.Home);
                Logger.Instance.Info($"NavigateTo 호출 후. CurrentMode: {_navigation.CurrentMode}, CurrentViewModel: {(_navigation.CurrentViewModel != null ? _navigation.CurrentViewModel.GetType().Name : "null")}");
                
                // 초기화 시에는 PropertyChanged가 발생하지 않을 수 있으므로 직접 ViewModel 생성
                Logger.Instance.Info($"강제 생성 체크 시작. CurrentViewModel == null: {_navigation.CurrentViewModel == null}");
                if (_navigation.CurrentViewModel == null)
                {
                    Logger.Instance.Info($"HomeViewModel 생성 시작");
                    var homeViewModel = new HomeViewModel(_navigation);
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
                            var homeViewModel = new HomeViewModel(_navigation);
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
                            var manualVerificationViewModel = new ManualVerificationViewModel(_navigation);
                            _navigation.SetManualVerificationViewModel(manualVerificationViewModel);
                            Services.Logger.Instance.Info($"MainNavigationViewModel: ManualVerificationViewModel 생성 완료");
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
