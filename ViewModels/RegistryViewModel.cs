using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 수험생 명렬 관리 ViewModel입니다.
    /// </summary>
    public class RegistryViewModel : INotifyPropertyChanged
    {
        private readonly NavigationViewModel _navigation;
        private readonly RegistryStore _registryStore;
        private StudentRegistry _studentRegistry;
        private InterviewerRegistry _interviewerRegistry;
        private int _selectedTabIndex = 0;

        public RegistryViewModel(NavigationViewModel navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _registryStore = new RegistryStore();

            // 명부 로드
            _studentRegistry = _registryStore.LoadStudentRegistry();
            _interviewerRegistry = _registryStore.LoadInterviewerRegistry();

            NavigateToHomeCommand = new RelayCommand(() => _navigation.NavigateTo(ApplicationMode.Home));
            ExportStudentRegistryTemplateCommand = new RelayCommand(OnExportStudentRegistryTemplate);
            ExportInterviewerRegistryTemplateCommand = new RelayCommand(OnExportInterviewerRegistryTemplate);
            LoadStudentRegistryCommand = new RelayCommand(OnLoadStudentRegistry);
            LoadInterviewerRegistryCommand = new RelayCommand(OnLoadInterviewerRegistry);
            SaveStudentRegistryCommand = new RelayCommand(OnSaveStudentRegistry, () => StudentRegistry.Students.Count > 0);
            SaveInterviewerRegistryCommand = new RelayCommand(OnSaveInterviewerRegistry, () => InterviewerRegistry.Interviewers.Count > 0);

            Logger.Instance.Info("RegistryViewModel 초기화 완료");
        }

        /// <summary>
        /// 현재 선택된 탭 인덱스 (0: 수험생 명부, 1: 면접위원 명부)
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                _selectedTabIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStudentTabSelected));
                OnPropertyChanged(nameof(IsInterviewerTabSelected));
            }
        }

        /// <summary>
        /// 수험생 명부 탭이 선택되었는지 여부 (0번 탭)
        /// </summary>
        public bool IsStudentTabSelected => SelectedTabIndex == 0;

        /// <summary>
        /// 면접위원 명부 탭이 선택되었는지 여부 (1번 탭)
        /// </summary>
        public bool IsInterviewerTabSelected => SelectedTabIndex == 1;

        public ICommand NavigateToHomeCommand { get; }
        public ICommand ExportStudentRegistryTemplateCommand { get; }
        public ICommand ExportInterviewerRegistryTemplateCommand { get; }
        public ICommand LoadStudentRegistryCommand { get; }
        public ICommand LoadInterviewerRegistryCommand { get; }
        public ICommand SaveStudentRegistryCommand { get; }
        public ICommand SaveInterviewerRegistryCommand { get; }

        public NavigationViewModel Navigation => _navigation;

        public StudentRegistry StudentRegistry
        {
            get => _studentRegistry;
            private set
            {
                _studentRegistry = value;
                OnPropertyChanged();
                // Save 명령의 CanExecute 상태 업데이트
                if (SaveStudentRegistryCommand is RelayCommand cmd)
                {
                    cmd.RaiseCanExecuteChanged();
                }
            }
        }

        public InterviewerRegistry InterviewerRegistry
        {
            get => _interviewerRegistry;
            private set
            {
                _interviewerRegistry = value;
                OnPropertyChanged();
                // Save 명령의 CanExecute 상태 업데이트
                if (SaveInterviewerRegistryCommand is RelayCommand cmd)
                {
                    cmd.RaiseCanExecuteChanged();
                }
            }
        }

        private void OnExportStudentRegistryTemplate()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
                Title = "양식 파일 저장",
                FileName = "수험생_명부_양식.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    Logger.Instance.Info($"수험생 명부 양식 파일 내보내기 시작: {dialog.FileName}");
                    _registryStore.ExportStudentRegistryTemplate(dialog.FileName);
                    Logger.Instance.Info("수험생 명부 양식 파일 내보내기 완료");
                    MessageBox.Show("양식 파일을 내보냈습니다.", 
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("수험생 명부 양식 파일 내보내기 실패", ex);
                    MessageBox.Show($"양식 파일 내보내기 실패:\n{ex.Message}", 
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnExportInterviewerRegistryTemplate()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
                Title = "양식 파일 저장",
                FileName = "면접위원_명부_양식.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    Logger.Instance.Info($"면접위원 명부 양식 파일 내보내기 시작: {dialog.FileName}");
                    _registryStore.ExportInterviewerRegistryTemplate(dialog.FileName);
                    Logger.Instance.Info("면접위원 명부 양식 파일 내보내기 완료");
                    MessageBox.Show("양식 파일을 내보냈습니다.", 
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("면접위원 명부 양식 파일 내보내기 실패", ex);
                    MessageBox.Show($"양식 파일 내보내기 실패:\n{ex.Message}", 
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnLoadStudentRegistry()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
                Title = "수험생 명부 파일 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    Logger.Instance.Info($"수험생 명부 로드 시작: {dialog.FileName}");
                    var registry = _registryStore.LoadStudentRegistryFromXlsx(dialog.FileName);
                    StudentRegistry = registry;
                    
                    // 저장도 자동으로 수행
                    _registryStore.SaveStudentRegistry(registry);
                    
                    Logger.Instance.Info($"수험생 명부 로드 완료: {registry.Students.Count}명");
                    MessageBox.Show($"수험생 명부를 불러왔습니다. (총 {registry.Students.Count}명)", 
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("수험생 명부 로드 실패", ex);
                    MessageBox.Show($"수험생 명부 로드 실패:\n{ex.Message}", 
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnLoadInterviewerRegistry()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
                Title = "면접위원 명부 파일 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    Logger.Instance.Info($"면접위원 명부 로드 시작: {dialog.FileName}");
                    var registry = _registryStore.LoadInterviewerRegistryFromXlsx(dialog.FileName);
                    InterviewerRegistry = registry;
                    
                    // 저장도 자동으로 수행
                    _registryStore.SaveInterviewerRegistry(registry);
                    
                    Logger.Instance.Info($"면접위원 명부 로드 완료: {registry.Interviewers.Count}명");
                    MessageBox.Show($"면접위원 명부를 불러왔습니다. (총 {registry.Interviewers.Count}명)", 
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("면접위원 명부 로드 실패", ex);
                    MessageBox.Show($"면접위원 명부 로드 실패:\n{ex.Message}", 
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnSaveStudentRegistry()
        {
            try
            {
                Logger.Instance.Info("수험생 명부 저장 시작");
                _registryStore.SaveStudentRegistry(StudentRegistry);
                Logger.Instance.Info($"수험생 명부 저장 완료: {StudentRegistry.Students.Count}명");
                MessageBox.Show("수험생 명부가 저장되었습니다.", 
                    "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("수험생 명부 저장 실패", ex);
                MessageBox.Show($"수험생 명부 저장 실패:\n{ex.Message}", 
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSaveInterviewerRegistry()
        {
            try
            {
                Logger.Instance.Info("면접위원 명부 저장 시작");
                _registryStore.SaveInterviewerRegistry(InterviewerRegistry);
                Logger.Instance.Info($"면접위원 명부 저장 완료: {InterviewerRegistry.Interviewers.Count}명");
                MessageBox.Show("면접위원 명부가 저장되었습니다.", 
                    "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("면접위원 명부 저장 실패", ex);
                MessageBox.Show($"면접위원 명부 저장 실패:\n{ex.Message}", 
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
