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
        private readonly TemplateStore _templateStore;
        private OmrTemplate _template;

        public TemplateViewModel(StateStore stateStore, OmrTemplate template)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _templateStore = new TemplateStore();
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
                    _templateStore.Export(Template, dialog.FileName);
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
                    var importedTemplate = _templateStore.Import(dialog.FileName);
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
                        // 고정 슬롯 정책: 컬렉션 Clear/Add 금지. 슬롯 좌표/크기만 초기화
                        ResetTimingSlots(Template);
                        ResetBarcodeSlots(Template);
                        ResetScoringSlots(Template);

                        // 가져온 템플릿 적용
                        Template.ReferenceWidth = importedTemplate.ReferenceWidth;
                        Template.ReferenceHeight = importedTemplate.ReferenceHeight;
                        
                        ApplyImportedTimingMarks(Template, importedTemplate);
                        
                        // ScoringAreas는 슬롯 구조(Questions.Options)에 적용해야 함
                        ApplyImportedScoringAreas(Template, importedTemplate);

                        ApplyImportedBarcodeAreas(Template, importedTemplate);

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

        private static void ResetScoringSlots(OmrTemplate template)
        {
            foreach (var question in template.Questions)
            {
                foreach (var slot in question.Options)
                {
                    slot.X = 0;
                    slot.Y = 0;
                    slot.Width = 0;
                    slot.Height = 0;
                    slot.StrokeThickness = 2.0;
                    slot.OverlayType = OverlayType.ScoringArea;
                    // OptionNumber/QuestionNumber는 고정 슬롯의 Identity이므로 유지
                }
            }
        }

        private static void ResetTimingSlots(OmrTemplate template)
        {
            foreach (var slot in template.TimingMarks)
            {
                slot.X = 0;
                slot.Y = 0;
                slot.Width = 0;
                slot.Height = 0;
                slot.StrokeThickness = 2.0;
                slot.OverlayType = OverlayType.TimingMark;
            }
        }

        private static void ResetBarcodeSlots(OmrTemplate template)
        {
            foreach (var slot in template.BarcodeAreas)
            {
                slot.X = 0;
                slot.Y = 0;
                slot.Width = 0;
                slot.Height = 0;
                slot.StrokeThickness = 2.0;
                slot.OverlayType = OverlayType.BarcodeArea;
            }
        }

        private static void ApplyImportedTimingMarks(OmrTemplate target, OmrTemplate imported)
        {
            foreach (var importedSlot in imported.TimingMarks)
            {
                if (!importedSlot.OptionNumber.HasValue) continue;
                var slot = target.TimingMarks.FirstOrDefault(o => o.OptionNumber == importedSlot.OptionNumber.Value);
                if (slot == null) continue;

                slot.OverlayType = OverlayType.TimingMark;
                slot.X = importedSlot.X;
                slot.Y = importedSlot.Y;
                slot.Width = importedSlot.Width;
                slot.Height = importedSlot.Height;
                slot.StrokeThickness = importedSlot.StrokeThickness;
            }
        }

        private static void ApplyImportedBarcodeAreas(OmrTemplate target, OmrTemplate imported)
        {
            foreach (var importedSlot in imported.BarcodeAreas)
            {
                if (!importedSlot.OptionNumber.HasValue) continue;
                var slot = target.BarcodeAreas.FirstOrDefault(o => o.OptionNumber == importedSlot.OptionNumber.Value);
                if (slot == null) continue;

                slot.OverlayType = OverlayType.BarcodeArea;
                slot.X = importedSlot.X;
                slot.Y = importedSlot.Y;
                slot.Width = importedSlot.Width;
                slot.Height = importedSlot.Height;
                slot.StrokeThickness = importedSlot.StrokeThickness;
            }
        }

        private static void ApplyImportedScoringAreas(OmrTemplate target, OmrTemplate imported)
        {
            // 새 형식: Questions 기반 (OptionNumber 매핑)
            if (imported.Questions.Count > 0)
            {
                foreach (var importedQuestion in imported.Questions)
                {
                    var question = target.Questions.FirstOrDefault(q => q.QuestionNumber == importedQuestion.QuestionNumber);
                    if (question == null)
                    {
                        question = new Question { QuestionNumber = importedQuestion.QuestionNumber };
                        target.Questions.Add(question);
                    }

                    foreach (var overlay in importedQuestion.Options)
                    {
                        if (overlay.OptionNumber.HasValue)
                        {
                            var slot = question.Options.FirstOrDefault(o => o.OptionNumber == overlay.OptionNumber.Value);
                            if (slot != null)
                            {
                                slot.X = overlay.X;
                                slot.Y = overlay.Y;
                                slot.Width = overlay.Width;
                                slot.Height = overlay.Height;
                                slot.StrokeThickness = overlay.StrokeThickness;
                                slot.OverlayType = OverlayType.ScoringArea;
                            }
                            else
                            {
                                // 고정 슬롯 정책: 슬롯이 없으면 무시
                                continue;
                            }
                        }
                        else
                        {
                            // 하위 호환: OptionNumber가 없으면 빈 슬롯에 순차 배치
                            var emptySlot = question.Options.FirstOrDefault(o => !o.IsPlaced);
                            if (emptySlot != null)
                            {
                                emptySlot.X = overlay.X;
                                emptySlot.Y = overlay.Y;
                                emptySlot.Width = overlay.Width;
                                emptySlot.Height = overlay.Height;
                                emptySlot.StrokeThickness = overlay.StrokeThickness;
                                emptySlot.OverlayType = OverlayType.ScoringArea;
                            }
                        }
                    }
                }

                return;
            }

            // 하위 호환: ScoringAreas만 있는 경우
            if (imported.ScoringAreas.Count > 0)
            {
                int questionsCount = OmrConstants.QuestionsCount;
                int optionsPerQuestion = OmrConstants.OptionsPerQuestion;
                var scoringAreasList = imported.ScoringAreas.ToList();

                for (int q = 0; q < questionsCount; q++)
                {
                    var question = target.Questions.FirstOrDefault(qu => qu.QuestionNumber == q + 1);
                    if (question == null)
                    {
                        question = new Question { QuestionNumber = q + 1 };
                        target.Questions.Add(question);
                    }

                    int startIndex = q * optionsPerQuestion;
                    for (int o = 0; o < optionsPerQuestion && startIndex + o < scoringAreasList.Count; o++)
                    {
                        var overlay = scoringAreasList[startIndex + o];
                        var slot = question.Options.FirstOrDefault(s => s.OptionNumber == o + 1);
                        if (slot != null)
                        {
                            slot.X = overlay.X;
                            slot.Y = overlay.Y;
                            slot.Width = overlay.Width;
                            slot.Height = overlay.Height;
                            slot.StrokeThickness = overlay.StrokeThickness;
                            slot.OverlayType = OverlayType.ScoringArea;
                        }
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

