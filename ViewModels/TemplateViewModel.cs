using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 템플릿 관리 전용 ViewModel입니다.
    /// </summary>
    public class TemplateViewModel : INotifyPropertyChanged
    {
        private readonly StateStore _stateStore;
        private OmrTemplate _template;

        public TemplateViewModel(StateStore stateStore, OmrTemplate template)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _template = template ?? throw new ArgumentNullException(nameof(template));

            ExportTemplateCommand = new RelayCommand(OnExportTemplate);
            ImportTemplateCommand = new RelayCommand(OnImportTemplate);
        }

        public OmrTemplate Template
        {
            get => _template;
            set
            {
                _template = value ?? throw new ArgumentNullException(nameof(value));
                OnPropertyChanged();
            }
        }

        public ICommand ExportTemplateCommand { get; }
        public ICommand ImportTemplateCommand { get; }

        /// <summary>
        /// 현재 템플릿을 파일로 내보냅니다.
        /// </summary>
        private void OnExportTemplate()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
                    FileName = $"template_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                    Title = "템플릿 내보내기"
                };

                if (dialog.ShowDialog() == true)
                {
                    // ExportTemplate은 StateStore에 있지만, TemplateStore를 사용하는 것이 더 적절함
                    // 일단 StateStore.ExportTemplate을 사용 (기존 코드 유지)
                    _stateStore.ExportTemplate(Template, dialog.FileName);
                    Logger.Instance.Info($"템플릿 내보내기 완료: {dialog.FileName}");
                    MessageBox.Show(
                        "템플릿이 파일로 저장되었습니다.",
                        "내보내기 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("템플릿 내보내기 실패", ex);
                MessageBox.Show($"템플릿 내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 파일에서 템플릿을 가져옵니다.
        /// </summary>
        private void OnImportTemplate()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*",
                    Title = "템플릿 가져오기"
                };

                if (dialog.ShowDialog() == true)
                {
                    // ImportTemplate은 StateStore에 있지만, TemplateStore를 사용하는 것이 더 적절함
                    // 일단 StateStore.ImportTemplate을 사용 (기존 코드 유지)
                    var importedTemplate = _stateStore.ImportTemplate(dialog.FileName);
                    if (importedTemplate == null)
                    {
                        MessageBox.Show(
                            "템플릿 파일을 읽을 수 없거나 형식이 올바르지 않습니다.",
                            "가져오기 실패",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    var result = MessageBox.Show(
                        "템플릿을 가져오면 현재 템플릿이 덮어씌워집니다.\n계속하시겠습니까?",
                        "확인",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 현재 템플릿 초기화
                        Template.TimingMarks.Clear();
                        Template.BarcodeAreas.Clear();
                        // Questions를 먼저 초기화하면 ScoringAreas도 자동으로 초기화됨
                        foreach (var question in Template.Questions)
                        {
                            question.Options.Clear();
                        }

                        // 가져온 템플릿 적용
                        Template.ReferenceWidth = importedTemplate.ReferenceWidth;
                        Template.ReferenceHeight = importedTemplate.ReferenceHeight;
                        
                        // TimingMarks 복원
                        foreach (var overlay in importedTemplate.TimingMarks)
                        {
                            Template.TimingMarks.Add(new RectangleOverlay
                            {
                                X = overlay.X,
                                Y = overlay.Y,
                                Width = overlay.Width,
                                Height = overlay.Height,
                                StrokeThickness = overlay.StrokeThickness,
                                OverlayType = overlay.OverlayType
                            });
                        }
                        
                        // Questions 복원 (중요: ScoringAreas는 자동으로 동기화됨)
                        foreach (var importedQuestion in importedTemplate.Questions)
                        {
                            var question = Template.Questions.FirstOrDefault(q => q.QuestionNumber == importedQuestion.QuestionNumber);
                            if (question == null)
                            {
                                // 문항이 없으면 새로 생성 (일반적으로는 4개가 이미 있음)
                                question = new Question { QuestionNumber = importedQuestion.QuestionNumber };
                                Template.Questions.Add(question);
                            }

                            // Options 복원
                            foreach (var overlay in importedQuestion.Options)
                            {
                                question.Options.Add(new RectangleOverlay
                                {
                                    X = overlay.X,
                                    Y = overlay.Y,
                                    Width = overlay.Width,
                                    Height = overlay.Height,
                                    StrokeThickness = overlay.StrokeThickness,
                                    OverlayType = overlay.OverlayType
                                });
                            }
                        }
                        // 하위 호환성: Questions가 없고 ScoringAreas만 있는 경우 처리
                        if (importedTemplate.Questions.Count == 0 && importedTemplate.ScoringAreas.Count > 0)
                        {
                            // ScoringAreas를 4문항 × 12선택지로 분할
                            const int questionsCount = 4;
                            const int optionsPerQuestion = 12;
                            var scoringAreasList = importedTemplate.ScoringAreas.ToList();
                            
                            for (int q = 0; q < questionsCount; q++)
                            {
                                var question = Template.Questions.FirstOrDefault(qu => qu.QuestionNumber == q + 1);
                                if (question == null)
                                {
                                    question = new Question { QuestionNumber = q + 1 };
                                    Template.Questions.Add(question);
                                }

                                int startIndex = q * optionsPerQuestion;
                                for (int o = 0; o < optionsPerQuestion && startIndex + o < scoringAreasList.Count; o++)
                                {
                                    var overlay = scoringAreasList[startIndex + o];
                                    question.Options.Add(new RectangleOverlay
                                    {
                                        X = overlay.X,
                                        Y = overlay.Y,
                                        Width = overlay.Width,
                                        Height = overlay.Height,
                                        StrokeThickness = overlay.StrokeThickness,
                                        OverlayType = overlay.OverlayType
                                    });
                                }
                            }
                        }

                        // BarcodeAreas 복원
                        foreach (var overlay in importedTemplate.BarcodeAreas)
                        {
                            Template.BarcodeAreas.Add(new RectangleOverlay
                            {
                                X = overlay.X,
                                Y = overlay.Y,
                                Width = overlay.Width,
                                Height = overlay.Height,
                                StrokeThickness = overlay.StrokeThickness,
                                OverlayType = overlay.OverlayType
                            });
                        }

                        Logger.Instance.Info("템플릿 가져오기 완료");
                        MessageBox.Show(
                            "템플릿이 가져와졌습니다.",
                            "가져오기 완료",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("템플릿 가져오기 실패", ex);
                MessageBox.Show($"템플릿 가져오기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

