using System;
using System.Collections.Specialized;
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
    /// 정답 및 배점 관리 ViewModel입니다.
    /// </summary>
    public class ScoringRuleViewModel : INotifyPropertyChanged
    {
        private readonly NavigationViewModel _navigation;
        private readonly ScoringRuleStore _scoringRuleStore;
        private ScoringRule _scoringRule;

        public ScoringRuleViewModel(NavigationViewModel navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _scoringRuleStore = new ScoringRuleStore();

            // 배점 정보 로드
            _scoringRule = _scoringRuleStore.LoadScoringRule();

            NavigateToHomeCommand = new RelayCommand(() => _navigation.NavigateTo(ApplicationMode.Home));
            LoadFromFileCommand = new RelayCommand(OnLoadFromFile);
            ExportTemplateCommand = new RelayCommand(OnExportTemplate);
            SaveCommand = new RelayCommand(OnSave);

            // ScoringRule 변경 시 자동 저장
            _scoringRule.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScoringRule.Questions) || e.PropertyName == nameof(ScoringRule.ScoreNames))
                {
                    AutoSave();
                }
            };

            // Questions의 각 항목 변경 시에도 자동 저장
            foreach (var question in _scoringRule.Questions)
            {
                question.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(QuestionScoringRule.Scores))
                    {
                        AutoSave();
                    }
                };
            }

            // ScoreNames 변경 감지
            _scoringRule.ScoreNames.CollectionChanged += (s, e) => AutoSave();

            Logger.Instance.Info("ScoringRuleViewModel 초기화 완료");
        }

        public ICommand NavigateToHomeCommand { get; }
        public ICommand LoadFromFileCommand { get; }
        public ICommand ExportTemplateCommand { get; }
        public ICommand SaveCommand { get; }

        public NavigationViewModel Navigation => _navigation;

        public ScoringRule ScoringRule
        {
            get => _scoringRule;
            private set
            {
                // 기존 이벤트 구독 해제
                if (_scoringRule != null)
                {
                    _scoringRule.PropertyChanged -= (s, e) => { };
                    _scoringRule.ScoreNames.CollectionChanged -= (s, e) => { };
                    foreach (var question in _scoringRule.Questions)
                    {
                        question.PropertyChanged -= (s, e) => { };
                    }
                }

                _scoringRule = value;
                OnPropertyChanged();

                // 새 ScoringRule에 이벤트 구독
                if (_scoringRule != null)
                {
                    _scoringRule.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(ScoringRule.Questions) || e.PropertyName == nameof(ScoringRule.ScoreNames))
                        {
                            AutoSave();
                        }
                    };

                    _scoringRule.ScoreNames.CollectionChanged += (s, e) => AutoSave();

                    foreach (var question in _scoringRule.Questions)
                    {
                        question.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(QuestionScoringRule.Scores))
                            {
                                AutoSave();
                            }
                        };
                    }
                }
            }
        }

        private void OnLoadFromFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
                Title = "정답 및 배점 파일 선택"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    Logger.Instance.Info($"정답 및 배점 로드 시작: {dialog.FileName}");
                    var scoringRule = _scoringRuleStore.LoadScoringRuleFromXlsx(dialog.FileName);
                    ScoringRule = scoringRule;
                    
                    Logger.Instance.Info("정답 및 배점 로드 완료");
                    MessageBox.Show("정답 및 배점 정보를 불러왔습니다.", 
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("정답 및 배점 로드 실패", ex);

                    // 정수-only/양수-only 정책 위반(소수점, 0, 음수 포함 등)
                    if (ex is InvalidOperationException && ex.Message.Contains("1 이상의 정수 점수만 허용"))
                    {
                        MessageBox.Show(
                            "점수는 1 이상의 정수만 입력해야 합니다.\n\n" +
                            "불러오려는 파일에 소수점/0/음수 점수가 포함되어 있어 중단했습니다.\n\n" +
                            ex.Message,
                            "점수 입력 오류 (파일 불러오기)",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    MessageBox.Show($"정답 및 배점 로드 실패:\n{ex.Message}",
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AutoSave()
        {
            try
            {
                _scoringRuleStore.SaveScoringRule(ScoringRule);
                Logger.Instance.Debug("정답 및 배점 자동 저장 완료");
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"정답 및 배점 자동 저장 실패: {ex.Message}");
            }
        }

        private void OnSave()
        {
            try
            {
                Logger.Instance.Info("정답 및 배점 저장 시작");
                _scoringRuleStore.SaveScoringRule(ScoringRule);
                Logger.Instance.Info("정답 및 배점 저장 완료");
                MessageBox.Show("정답 및 배점 정보가 저장되었습니다.", 
                    "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("정답 및 배점 저장 실패", ex);
                MessageBox.Show($"정답 및 배점 저장 실패:\n{ex.Message}", 
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnExportTemplate()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
                Title = "양식 파일 저장",
                FileName = "문항_배점_양식.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    Logger.Instance.Info($"양식 파일 내보내기 시작: {dialog.FileName}");
                    _scoringRuleStore.ExportTemplate(dialog.FileName);
                    Logger.Instance.Info("양식 파일 내보내기 완료");
                    MessageBox.Show("양식 파일을 내보냈습니다.", 
                        "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("양식 파일 내보내기 실패", ex);
                    MessageBox.Show($"양식 파일 내보내기 실패:\n{ex.Message}", 
                        "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
