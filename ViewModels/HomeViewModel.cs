using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Views;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 홈 화면용 ViewModel입니다.
    /// </summary>
    public class HomeViewModel : INotifyPropertyChanged, INavigationAware
    {
        private readonly NavigationViewModel _navigation;
        private readonly AppStateStore _appStateStore;
        private readonly StateStore _stateStore;
        private readonly Workspace _workspace;
        private readonly DataUsageService _dataUsageService;
        private string? _selectedRound;
        private bool _isScanning;
        private System.Threading.CancellationTokenSource? _scanCancellation;
        
        // 회차별 마지막 스캔 시간 추적
        private readonly Dictionary<string, DateTime> _lastScanTimes = new();
        // 회차별 마지막 스캔 결과 캐시
        private readonly Dictionary<string, List<DataUsageItem>> _cachedDataUsage = new();

        public HomeViewModel(NavigationViewModel navigation, Workspace workspace, StateStore stateStore)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _appStateStore = new AppStateStore();
            _dataUsageService = new DataUsageService();

            // 회차 목록 로드
            LoadRounds();

            // 현재 선택된 회차 설정
            _selectedRound = PathService.CurrentRound;
            if (string.IsNullOrEmpty(_selectedRound) && AvailableRounds.Count > 0)
            {
                _selectedRound = AvailableRounds[0];
                PathService.CurrentRound = _selectedRound;
            }

            NavigateToTemplateEditCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(ApplicationMode.TemplateEdit);
            });

            NavigateToMarkingCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(ApplicationMode.Marking);
            });

            NavigateToRegistryCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(ApplicationMode.Registry);
            });

            NavigateToGradingCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(ApplicationMode.Grading);
            });

            NavigateToScoringRuleCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(ApplicationMode.ScoringRule);
            });

            NavigateToManualVerificationCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(ApplicationMode.ManualVerification);
            });

            CreateRoundCommand = new RelayCommand(OnCreateRound);
            RenameRoundCommand = new RelayCommand(OnRenameRound, () => !string.IsNullOrEmpty(SelectedRound));
            RefreshDataUsageCommand = new RelayCommand(OnRefreshDataUsage, () => !IsScanning && !string.IsNullOrEmpty(SelectedRound));

            // 초기 데이터 로드 (프로그램 시작 시)
            if (!string.IsNullOrEmpty(_selectedRound))
            {
                // UI가 완전히 로드된 후 실행하기 위해 비동기로 처리
                Application.Current?.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    CheckAndRefreshIfNeeded(_selectedRound);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 화면 진입 시 호출됩니다.
        /// </summary>
        public void OnNavigatedTo(object? parameter)
        {
            // 화면 진입 시 데이터 변경 여부 확인 후 필요시 새로고침
            if (!string.IsNullOrEmpty(SelectedRound))
            {
                CheckAndRefreshIfNeeded(SelectedRound);
            }
        }

        /// <summary>
        /// 화면 이탈 시 호출됩니다.
        /// </summary>
        public void OnNavigatedFrom()
        {
            // 필요시 정리 작업
        }

        /// <summary>
        /// 사용 가능한 회차 목록
        /// </summary>
        public ObservableCollection<string> AvailableRounds { get; } = new ObservableCollection<string>();

        /// <summary>
        /// 현재 선택된 회차
        /// </summary>
        public string? SelectedRound
        {
            get => _selectedRound;
            set
            {
                if (_selectedRound != value)
                {
                    _selectedRound = value;
                    OnPropertyChanged();
                    RenameRoundCommand.RaiseCanExecuteChanged();

                    if (!string.IsNullOrEmpty(value))
                    {
                        ApplyRoundChange(value);

                        // 데이터 변경 여부 확인 후 필요시 새로고침
                        CheckAndRefreshIfNeeded(value);
                    }
                }
            }
        }

        /// <summary>
        /// 데이터 변경 여부를 확인하고 필요시 새로고침합니다.
        /// </summary>
        private void CheckAndRefreshIfNeeded(string roundName)
        {
            if (string.IsNullOrEmpty(roundName))
                return;

            // 마지막 스캔 시간 확인
            var lastScanTime = _lastScanTimes.GetValueOrDefault(roundName, DateTime.MinValue);
            
            // 마지막 스캔 이후 파일이 변경되었는지 확인
            bool needsRefresh = false;
            
            if (lastScanTime == DateTime.MinValue)
            {
                // 첫 스캔이면 새로고침 필요
                needsRefresh = true;
            }
            else
            {
                // 주요 파일들의 마지막 수정 시간 확인
                var roundRoot = PathService.GetRoundRoot(roundName);
                var filesToCheck = new[]
                {
                    Path.Combine(roundRoot, "template.json"),
                    Path.Combine(roundRoot, "scoring_rule.json"),
                    Path.Combine(roundRoot, "state.json"),
                    Path.Combine(roundRoot, "session.json")
                };

                foreach (var filePath in filesToCheck)
                {
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            if (fileInfo.LastWriteTime > lastScanTime)
                            {
                                needsRefresh = true;
                                break;
                            }
                        }
                        catch
                        {
                            // 파일 접근 실패 시 무시
                        }
                    }
                }

                // 폴더들도 확인 (간단하게 폴더 자체의 LastWriteTime만 확인)
                if (!needsRefresh)
                {
                    var foldersToCheck = new[]
                    {
                        Path.Combine(roundRoot, "aligned_cache"),
                        Path.Combine(roundRoot, "output"),
                        Path.Combine(roundRoot, "barcode_debug")
                    };

                    foreach (var folderPath in foldersToCheck)
                    {
                        if (Directory.Exists(folderPath))
                        {
                            try
                            {
                                var dirInfo = new DirectoryInfo(folderPath);
                                if (dirInfo.LastWriteTime > lastScanTime)
                                {
                                    needsRefresh = true;
                                    break;
                                }
                            }
                            catch
                            {
                                // 폴더 접근 실패 시 무시
                            }
                        }
                    }
                }
            }

            if (needsRefresh)
            {
                // 변경이 있으면 새로고침
                RefreshDataUsageAsync(roundName);
            }
            else if (_cachedDataUsage.TryGetValue(roundName, out var cached))
            {
                // 변경이 없으면 캐시된 데이터 복원
                DataUsageItems.Clear();
                foreach (var item in cached)
                {
                    DataUsageItems.Add(item);
                }
                Logger.Instance.Debug($"데이터 변경 없음, 캐시된 데이터 사용: {roundName}");
            }
        }

        private void ApplyRoundChange(string roundName)
        {
            // 회차 변경 시 PathService.CurrentRound 업데이트
            PathService.CurrentRound = roundName;
            _appStateStore.UpdateRoundAccessTime(roundName);

            // Workspace 재로드
            try
            {
                var newWorkspace = _stateStore.Load();
                _workspace.InputFolderPath = newWorkspace.InputFolderPath;
                _workspace.SelectedDocumentId = newWorkspace.SelectedDocumentId;
                _workspace.Template = newWorkspace.Template;
                Logger.Instance.Info($"회차 변경: {roundName}, Workspace 재로드 완료");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"회차 변경 시 Workspace 재로드 실패: {ex.Message}", ex);
                MessageBox.Show(
                    $"회차 변경 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void LoadRounds()
        {
            AvailableRounds.Clear();
            var appState = _appStateStore.LoadAppState();

            // 회차 자동 발견
            var discoveredRounds = _appStateStore.DiscoverRounds();
            foreach (var discovered in discoveredRounds)
            {
                if (!appState.Rounds.Any(r => r.Name.Equals(discovered.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    appState.Rounds.Add(discovered);
                }
            }

            // 회차 목록 정렬 (CreatedAt 기준 내림차순 - 최신 생성이 위로)
            var sortedRounds = appState.Rounds
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.Name)
                .ToList();

            foreach (var round in sortedRounds)
            {
                AvailableRounds.Add(round);
            }

            // app_state.json 저장 (발견된 회차 반영)
            if (discoveredRounds.Count > 0)
            {
                _appStateStore.SaveAppState(appState);
            }
        }

        private void OnCreateRound()
        {
            var dialog = new InputDialog("새 회차 이름을 입력하세요:", "회차 추가", "");
            dialog.Owner = Application.Current.MainWindow;
            
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Result))
            {
                var roundName = dialog.Result.Trim();
                try
                {
                    var created = _appStateStore.CreateRound(roundName);
                    LoadRounds();
                    SelectedRound = created.Name;
                    MessageBox.Show(
                        $"회차 '{created.Name}'가 생성되었습니다.",
                        "회차 생성",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"회차 생성 실패: {ex.Message}", ex);
                    MessageBox.Show(
                        $"회차 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                        "오류",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void OnRenameRound()
        {
            if (string.IsNullOrEmpty(SelectedRound))
            {
                return;
            }

            var dialog = new InputDialog(
                $"회차 이름을 변경합니다.\n현재 이름: {SelectedRound}\n새 이름을 입력하세요:",
                "회차 이름 변경",
                SelectedRound);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Result))
            {
                var newName = dialog.Result.Trim();
                if (newName.Equals(SelectedRound, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                try
                {
                    _appStateStore.RenameRound(SelectedRound, newName);
                    LoadRounds();
                    SelectedRound = newName;
                    MessageBox.Show(
                        $"회차 이름이 '{newName}'로 변경되었습니다.",
                        "회차 이름 변경",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"회차 이름 변경 실패: {ex.Message}", ex);
                    MessageBox.Show(
                        $"회차 이름 변경 중 오류가 발생했습니다:\n\n{ex.Message}",
                        "오류",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

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
        /// 새 회차 생성
        /// </summary>
        public ICommand CreateRoundCommand { get; }

        /// <summary>
        /// 회차 이름 변경
        /// </summary>
        public RelayCommand RenameRoundCommand { get; }

        /// <summary>
        /// 데이터 사용 정보 목록
        /// </summary>
        public ObservableCollection<DataUsageItem> DataUsageItems { get; } = new ObservableCollection<DataUsageItem>();

        /// <summary>
        /// 스캔 중 여부
        /// </summary>
        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                if (_isScanning != value)
                {
                    _isScanning = value;
                    OnPropertyChanged();
                    RefreshDataUsageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 데이터 사용 정보 새로고침 커맨드
        /// </summary>
        public RelayCommand RefreshDataUsageCommand { get; }

        private async void RefreshDataUsageAsync(string roundName)
        {
            // 이전 스캔 취소
            _scanCancellation?.Cancel();
            _scanCancellation?.Dispose();
            _scanCancellation = new System.Threading.CancellationTokenSource();

            IsScanning = true;
            DataUsageItems.Clear();

            try
            {
                // MainWindow가 표시되지 않았을 수 있으므로 null 전달 (ProgressRunner가 자동 처리)
                var mainWindow = Application.Current?.MainWindow;
                var owner = mainWindow != null && mainWindow.IsLoaded ? mainWindow : null;
                
                var wasCancelled = await ProgressRunner.RunAsync(
                    owner,
                    async scope =>
                    {
                        var result = await _dataUsageService.ScanRoundDataUsageAsync(
                            roundName,
                            scope.CancellationToken,
                            (current, total, status) =>
                            {
                                scope.Report(current, total, status);
                            });

                        scope.Ui(() =>
                        {
                            DataUsageItems.Clear();
                            
                            // 가장 최근에 변경된 항목 찾기 (로그 제외)
                            var mostRecentItem = result
                                .Where(item => item.LastModified.HasValue && item.Name != "로그")
                                .OrderByDescending(item => item.LastModified)
                                .FirstOrDefault();
                            
                            foreach (var item in result)
                            {
                                if (item == mostRecentItem)
                                {
                                    item.IsMostRecent = true;
                                }
                                DataUsageItems.Add(item);
                            }

                            // 스캔 결과 캐시 및 스캔 시간 기록
                            _cachedDataUsage[roundName] = result.ToList(); // 복사본 저장
                            _lastScanTimes[roundName] = DateTime.Now;
                        });
                    },
                    "데이터 정보 스캔",
                    "데이터 정보를 확인하는 중...");

                if (wasCancelled)
                {
                    // 취소됨
                    Logger.Instance.Info("데이터 정보 스캔이 취소되었습니다.");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"데이터 정보 스캔 실패: {ex.Message}", ex);
                MessageBox.Show(
                    $"데이터 정보를 확인하는 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void OnRefreshDataUsage()
        {
            if (!string.IsNullOrEmpty(SelectedRound))
            {
                RefreshDataUsageAsync(SelectedRound);
            }
        }

        /// <summary>
        /// 폴더를 탐색기에서 여는 명령
        /// </summary>
        public ICommand OpenFolderCommand => new RelayCommand<DataUsageItem>(item =>
        {
            if (item?.Type == DataUsageItem.ItemType.Folder && !string.IsNullOrEmpty(item.Path))
            {
                OpenFolderInExplorer(item.Path);
            }
        });

        private void OpenFolderInExplorer(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                MessageBox.Show(
                    "폴더를 찾을 수 없습니다.",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"탐색기 열기 실패: {ex.Message}", ex);
                MessageBox.Show(
                    $"탐색기를 열 수 없습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}










