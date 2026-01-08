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

            SaveAsDefaultTemplateCommand = new RelayCommand(OnSaveAsDefaultTemplate);
            LoadDefaultTemplateCommand = new RelayCommand(OnLoadDefaultTemplate);
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

        public ICommand SaveAsDefaultTemplateCommand { get; }
        public ICommand LoadDefaultTemplateCommand { get; }

        /// <summary>
        /// 현재 템플릿을 기본 템플릿으로 저장합니다.
        /// </summary>
        private void OnSaveAsDefaultTemplate()
        {
            try
            {
                _stateStore.SaveDefaultTemplate(Template);
                Logger.Instance.Info("기본 템플릿 저장 완료");
                MessageBox.Show(
                    "현재 템플릿이 기본 템플릿으로 저장되었습니다.\n다음에 프로그램을 시작할 때 이 템플릿이 자동으로 로드됩니다.",
                    "기본 템플릿 저장 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("기본 템플릿 저장 실패", ex);
                MessageBox.Show($"기본 템플릿 저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 기본 템플릿을 현재 템플릿으로 로드합니다.
        /// </summary>
        private void OnLoadDefaultTemplate()
        {
            try
            {
                var defaultTemplate = _stateStore.LoadDefaultTemplate();
                if (defaultTemplate == null)
                {
                    MessageBox.Show(
                        "저장된 기본 템플릿이 없습니다.\n먼저 '기본 템플릿으로 저장' 기능을 사용하여 템플릿을 저장하세요.",
                        "기본 템플릿 없음",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    "기본 템플릿을 로드하면 현재 템플릿이 덮어씌워집니다.\n계속하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 현재 템플릿 초기화
                    Template.TimingMarks.Clear();
                    Template.ScoringAreas.Clear();

                    // 기본 템플릿 로드
                    Template.ReferenceWidth = defaultTemplate.ReferenceWidth;
                    Template.ReferenceHeight = defaultTemplate.ReferenceHeight;
                    
                    foreach (var overlay in defaultTemplate.TimingMarks)
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
                    
                    foreach (var overlay in defaultTemplate.ScoringAreas)
                    {
                        Template.ScoringAreas.Add(new RectangleOverlay
                        {
                            X = overlay.X,
                            Y = overlay.Y,
                            Width = overlay.Width,
                            Height = overlay.Height,
                            StrokeThickness = overlay.StrokeThickness,
                            OverlayType = overlay.OverlayType
                        });
                    }

                    Logger.Instance.Info("기본 템플릿 로드 완료");
                    MessageBox.Show(
                        "기본 템플릿이 로드되었습니다.",
                        "로드 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("기본 템플릿 로드 실패", ex);
                MessageBox.Show($"기본 템플릿 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

