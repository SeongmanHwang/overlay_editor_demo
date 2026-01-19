using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public class HomeViewModel : INotifyPropertyChanged
    {
        private readonly NavigationViewModel _navigation;
        private readonly AppStateStore _appStateStore;
        private readonly StateStore _stateStore;
        private readonly Workspace _workspace;
        private string? _selectedRound;

        public HomeViewModel(NavigationViewModel navigation, Workspace workspace, StateStore stateStore)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _appStateStore = new AppStateStore();

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
                        // 회차 변경 시 PathService.CurrentRound 업데이트
                        PathService.CurrentRound = value;
                        _appStateStore.UpdateRoundAccessTime(value);

                        // Workspace 재로드
                        try
                        {
                            var newWorkspace = _stateStore.Load();
                            _workspace.InputFolderPath = newWorkspace.InputFolderPath;
                            _workspace.SelectedDocumentId = newWorkspace.SelectedDocumentId;
                            _workspace.Template = newWorkspace.Template;
                            Logger.Instance.Info($"회차 변경: {value}, Workspace 재로드 완료");
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
                }
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
                    _appStateStore.CreateRound(roundName);
                    LoadRounds();
                    SelectedRound = roundName;
                    MessageBox.Show(
                        $"회차 '{roundName}'가 생성되었습니다.",
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}










