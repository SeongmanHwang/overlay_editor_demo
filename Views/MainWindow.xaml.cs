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

                // 마이그레이션: 기존 단일 루트 데이터가 있으면 "기존 폴더" 회차로 이동
                if (!Directory.Exists(PathService.RoundsFolder))
                {
                    var hasLegacyData = File.Exists(Path.Combine(PathService.AppDataFolder, "state.json")) ||
                                       File.Exists(Path.Combine(PathService.AppDataFolder, "session.json")) ||
                                       File.Exists(Path.Combine(PathService.AppDataFolder, "template.json")) ||
                                       File.Exists(Path.Combine(PathService.AppDataFolder, "student_registry.json")) ||
                                       File.Exists(Path.Combine(PathService.AppDataFolder, "interviewer_registry.json")) ||
                                       File.Exists(Path.Combine(PathService.AppDataFolder, "scoring_rule.json")) ||
                                       Directory.Exists(Path.Combine(PathService.AppDataFolder, "aligned_cache")) ||
                                       Directory.Exists(Path.Combine(PathService.AppDataFolder, "output"));

                    if (hasLegacyData)
                    {
                        Logger.Instance.Info("기존 단일 루트 데이터 발견. '기존 폴더' 회차로 마이그레이션합니다.");
                        MigrateLegacyDataToRound(appStateStore, "기존 폴더");
                        // 마이그레이션 후 app_state 다시 로드
                        appState = appStateStore.LoadAppState();
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

                // 기본 회차 생성 (첫 실행 시, 마이그레이션 후가 아닐 때)
                // 마이그레이션 후에는 이미 "기존 폴더"가 생성되었을 수 있으므로 확인 필요
                if (appState.Rounds.Count == 0)
                {
                    Logger.Instance.Info("기본 회차 생성: 2027년, 2028년");
                    
                    // "2027년"이 이미 폴더로 존재하는지 확인
                    var has2027 = Directory.Exists(PathService.GetRoundRoot("2027년"));
                    var has2028 = Directory.Exists(PathService.GetRoundRoot("2028년"));
                    
                    if (!has2027)
                    {
                        appStateStore.CreateRound("2027년");
                    }
                    else
                    {
                        // 폴더는 있지만 app_state.json에 없는 경우 (마이그레이션 후)
                        appState.Rounds.Add(new Models.RoundInfo
                        {
                            Name = "2027년",
                            CreatedAt = Directory.GetCreationTimeUtc(PathService.GetRoundRoot("2027년")),
                            LastAccessedAt = Directory.GetLastWriteTimeUtc(PathService.GetRoundRoot("2027년"))
                        });
                        Logger.Instance.Info("기존 '2027년' 폴더 발견, 목록에 추가");
                    }
                    
                    if (!has2028)
                    {
                        appStateStore.CreateRound("2028년");
                    }
                    else
                    {
                        appState.Rounds.Add(new Models.RoundInfo
                        {
                            Name = "2028년",
                            CreatedAt = Directory.GetCreationTimeUtc(PathService.GetRoundRoot("2028년")),
                            LastAccessedAt = Directory.GetLastWriteTimeUtc(PathService.GetRoundRoot("2028년"))
                        });
                        Logger.Instance.Info("기존 '2028년' 폴더 발견, 목록에 추가");
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
                        .FirstOrDefault()?.Name ?? appState.Rounds.FirstOrDefault()?.Name ?? "2027년";
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

            // Navigation의 모드 변경 감지 (ViewModel 생성)
            _navigation.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_navigation.CurrentMode))
                {
                    if (_navigation.CurrentMode == ApplicationMode.Home && _navigation.CurrentViewModel == null)
                    {
                        // Home 모드로 전환 시 ViewModel 생성
                        var homeViewModel = new HomeViewModel(_navigation, _workspace, _stateStore);
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

        /// <summary>
        /// 기존 단일 루트 데이터를 지정된 회차로 마이그레이션합니다.
        /// </summary>
        private void MigrateLegacyDataToRound(AppStateStore appStateStore, string roundName)
        {
            try
            {
                // 회차 생성 (이미 있으면 스킵)
                var existingRound = appStateStore.LoadAppState().Rounds
                    .FirstOrDefault(r => r.Name.Equals(roundName, StringComparison.OrdinalIgnoreCase));

                if (existingRound == null)
                {
                    appStateStore.CreateRound(roundName);
                }

                var roundRoot = PathService.GetRoundRoot(roundName);
                Directory.CreateDirectory(roundRoot);
                PathService.EnsureRoundDirectories(roundName);

                var appDataFolder = PathService.AppDataFolder;

                // 파일 이동
                var filesToMove = new[]
                {
                    ("state.json", "state.json"),
                    ("session.json", "session.json"),
                    ("template.json", "template.json"),
                    ("student_registry.json", "student_registry.json"),
                    ("interviewer_registry.json", "interviewer_registry.json"),
                    ("scoring_rule.json", "scoring_rule.json")
                };

                foreach (var (sourceFile, destFile) in filesToMove)
                {
                    var sourcePath = Path.Combine(appDataFolder, sourceFile);
                    var destPath = Path.Combine(roundRoot, destFile);

                    if (File.Exists(sourcePath) && !File.Exists(destPath))
                    {
                        File.Move(sourcePath, destPath);
                        Logger.Instance.Info($"마이그레이션: {sourceFile} → {roundName}/{destFile}");
                    }
                }

                // 폴더 이동
                var foldersToMove = new[]
                {
                    ("aligned_cache", "aligned_cache"),
                    ("output", "output"),
                    ("barcode_debug", "barcode_debug")
                };

                foreach (var (sourceFolder, destFolder) in foldersToMove)
                {
                    var sourcePath = Path.Combine(appDataFolder, sourceFolder);
                    var destPath = Path.Combine(roundRoot, destFolder);

                    if (Directory.Exists(sourcePath) && !Directory.Exists(destPath))
                    {
                        // 폴더 내부 파일들을 이동
                        Directory.CreateDirectory(destPath);
                        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
                        {
                            var relativePath = Path.GetRelativePath(sourcePath, file);
                            var destFile = Path.Combine(destPath, relativePath);
                            var destDir = Path.GetDirectoryName(destFile);
                            if (!string.IsNullOrEmpty(destDir))
                            {
                                Directory.CreateDirectory(destDir);
                            }
                            File.Move(file, destFile);
                        }
                        Directory.Delete(sourcePath, true);
                        Logger.Instance.Info($"마이그레이션: {sourceFolder}/ → {roundName}/{destFolder}/");
                    }
                }

                // app_state.json 업데이트
                var appState = appStateStore.LoadAppState();
                appState.LastSelectedRound = roundName;
                appStateStore.SaveAppState(appState);

                Logger.Instance.Info($"마이그레이션 완료: {roundName}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"마이그레이션 실패: {ex.Message}", ex);
                // 마이그레이션 실패해도 계속 진행
            }
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
