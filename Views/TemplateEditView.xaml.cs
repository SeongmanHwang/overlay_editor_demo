
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SimpleOverlayEditor.ViewModels;
using SimpleOverlayEditor.Utils;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// TemplateEditView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TemplateEditView : UserControl
    {
        private TemplateEditViewModel ViewModel => (TemplateEditViewModel)DataContext;
        private bool _isUpdatingCanvas = false;
        private Models.ImageDocument? _currentSubscribedDocument;
        private PropertyChangedEventHandler? _documentPropertyChangedHandler;
        
        // 오버레이 속성 변경 구독 관리
        private RectangleOverlay? _subscribedOverlay;
        private readonly PropertyChangedEventHandler _overlayChangedHandler;

        public TemplateEditView()
        {
            InitializeComponent();

            // 핸들러를 필드로 고정하여 메모리 누수 방지
            _overlayChangedHandler = Overlay_PropertyChanged;

            // ViewModel이 설정된 후 이벤트 구독
            this.Loaded += TemplateEditView_Loaded;
            this.Unloaded += UserControl_Unloaded;
        }

        private void TemplateEditView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                
                if (ViewModel.Workspace?.Template != null)
                {
                    ViewModel.Workspace.Template.TimingMarks.CollectionChanged += (s, args) => DrawOverlays();
                    ViewModel.Workspace.Template.ScoringAreas.CollectionChanged += (s, args) => DrawOverlays();
                    ViewModel.Workspace.Template.BarcodeAreas.CollectionChanged += (s, args) => DrawOverlays();
                }

                // 초기 로드 시 안내 메시지 상태 업데이트
                UpdateNoImageHintVisibility();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                Logger.Instance.Debug($"TemplateEditView - ViewModel_PropertyChanged: {e.PropertyName}");

                if (e.PropertyName == nameof(ViewModel.SelectedDocument))
                {
                    // 이전 문서의 이벤트 핸들러 해제
                    if (_currentSubscribedDocument != null && _documentPropertyChangedHandler != null)
                    {
                        _currentSubscribedDocument.PropertyChanged -= _documentPropertyChangedHandler;
                        _documentPropertyChangedHandler = null;
                    }

                    UpdateImageDisplay();
                    DrawOverlays();

                    // 새 문서의 이벤트 핸들러 등록
                    if (ViewModel.SelectedDocument != null)
                    {
                        _currentSubscribedDocument = ViewModel.SelectedDocument;
                        _documentPropertyChangedHandler = (s, args) =>
                        {
                            if (args.PropertyName == nameof(ViewModel.SelectedDocument.ImageWidth) ||
                                args.PropertyName == nameof(ViewModel.SelectedDocument.ImageHeight))
                            {
                                UpdateImageDisplay();
                                DrawOverlays();
                            }
                        };
                        ViewModel.SelectedDocument.PropertyChanged += _documentPropertyChangedHandler;
                    }
                    else
                    {
                        _currentSubscribedDocument = null;
                    }

                    // 이미지 로드 상태에 따라 안내 메시지 표시/숨김
                    UpdateNoImageHintVisibility();
                }

                if (e.PropertyName == nameof(ViewModel.CurrentImageDisplayRect))
                {
                    DrawOverlays();
                }

                if (e.PropertyName == nameof(ViewModel.SelectedOverlay))
                {
                    // SelectedOverlay가 변경될 때마다 구독 재설정
                    ResubscribeOverlay(ViewModel.SelectedOverlay);
                    DrawOverlays();
                }

                if (e.PropertyName == nameof(ViewModel.DisplayOverlays) ||
                    e.PropertyName == nameof(ViewModel.CurrentOverlayCollection) ||
                    e.PropertyName == nameof(ViewModel.CurrentOverlayType))
                {
                    DrawOverlays();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"TemplateEditView - ViewModel_PropertyChanged에서 예외 발생", ex);
            }
        }

        private void ImageCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isUpdatingCanvas) return;
            UpdateImageDisplay();
            DrawOverlays();
        }

        private void ImageContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 이미지가 없을 때 안내 메시지가 중앙에 표시되도록 업데이트
            UpdateNoImageHintVisibility();
        }

        private void UpdateNoImageHintVisibility()
        {
            if (NoImageHint == null) return;

            if (ViewModel?.SelectedDocument == null)
            {
                NoImageHint.Visibility = Visibility.Visible;
            }
            else
            {
                NoImageHint.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateImageDisplay()
        {
            try
            {
                if (ViewModel.SelectedDocument == null)
                {
                    SourceImage.Source = null;
                    // Canvas 크기 초기화
                    ImageCanvas.Width = 0;
                    ImageCanvas.Height = 0;
                    // 이미지가 없을 때 안내 메시지 표시
                    UpdateNoImageHintVisibility();
                    return;
                }

                // 이미지가 있을 때 안내 메시지 숨김
                UpdateNoImageHintVisibility();

                // 템플릿 편집 모드는 원본 이미지만 사용 (정렬하지 않음)
                var imagePath = ViewModel.SelectedDocument.SourcePath;

                if (File.Exists(imagePath))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    SourceImage.Source = bitmap;
                }

                // 이미지 표시 영역 계산
                var viewportWidth = ImageScrollViewer.ViewportWidth;
                var viewportHeight = ImageScrollViewer.ViewportHeight;

                if (viewportWidth <= 0 || viewportHeight <= 0)
                {
                    return;
                }

                var availableSize = new Size(viewportWidth, viewportHeight);
                ViewModel.UpdateImageDisplayRect(availableSize);
                var displayRect = ViewModel.CurrentImageDisplayRect;

                // Canvas 크기 설정
                const double horizontalPadding = 10;
                const double verticalPadding = 10;

                var requiredCanvasWidth = Math.Max(displayRect.Width + horizontalPadding, availableSize.Width);
                var requiredCanvasHeight = displayRect.Height + verticalPadding;

                _isUpdatingCanvas = true;
                try
                {
                    ImageCanvas.Width = requiredCanvasWidth;
                    ImageCanvas.Height = requiredCanvasHeight;
                }
                finally
                {
                    _isUpdatingCanvas = false;
                }

                // Image 위치 및 크기 설정
                Canvas.SetLeft(SourceImage, 0);
                Canvas.SetTop(SourceImage, 0);
                SourceImage.Width = displayRect.Width;
                SourceImage.Height = displayRect.Height;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("TemplateEditView - UpdateImageDisplay 실패", ex);
            }
        }

        private void DrawOverlays()
        {
            try
            {
                // 기존 오버레이 제거
                var overlaysToRemove = ImageCanvas.Children.OfType<Rectangle>().ToList();
                foreach (var rect in overlaysToRemove)
                {
                    ImageCanvas.Children.Remove(rect);
                }

                if (ViewModel.SelectedDocument == null || ViewModel.Workspace?.Template == null)
                {
                    return;
                }

                var displayRect = ViewModel.CurrentImageDisplayRect;
                var doc = ViewModel.SelectedDocument;
                var template = ViewModel.Workspace.Template;

                var allOverlays = template.TimingMarks
                    .Concat(template.ScoringAreas)
                    .Concat(template.BarcodeAreas)
                    .ToList();

                if (doc.ImageWidth <= 0 || doc.ImageHeight <= 0 ||
                    displayRect.Width <= 0 || displayRect.Height <= 0)
                {
                    return;
                }

                // 스케일 계산 (Uniform이므로 가로/세로 동일)
                var scaleX = displayRect.Width / doc.ImageWidth;
                var scaleY = displayRect.Height / doc.ImageHeight;

                foreach (var overlay in allOverlays)
                {
                    try
                    {
                        var rect = new Rectangle
                        {
                            Stroke = Brushes.Red,
                            StrokeThickness = overlay.StrokeThickness * Math.Min(scaleX, scaleY),
                            Fill = Brushes.Transparent,
                            Width = overlay.Width * scaleX,
                            Height = overlay.Height * scaleY
                        };

                        Canvas.SetLeft(rect, displayRect.X + overlay.X * scaleX);
                        Canvas.SetTop(rect, displayRect.Y + overlay.Y * scaleY);

                        // 선택된 오버레이 강조
                        if (overlay == ViewModel.SelectedOverlay)
                        {
                            rect.Stroke = Brushes.Blue;
                            rect.StrokeThickness = (overlay.StrokeThickness + 2) * Math.Min(scaleX, scaleY);
                        }

                        // 오버레이 타입에 따라 색상 구분
                        if (overlay.OverlayType == Models.OverlayType.TimingMark)
                        {
                            rect.Stroke = overlay == ViewModel.SelectedOverlay ? Brushes.Blue : Brushes.Green;
                        }
                        else if (overlay.OverlayType == Models.OverlayType.ScoringArea)
                        {
                            rect.Stroke = overlay == ViewModel.SelectedOverlay ? Brushes.Blue : Brushes.Red;
                        }
                        else if (overlay.OverlayType == Models.OverlayType.BarcodeArea)
                        {
                            rect.Stroke = overlay == ViewModel.SelectedOverlay ? Brushes.Blue : Brushes.Orange;
                        }

                        ImageCanvas.Children.Add(rect);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error($"TemplateEditView - 오버레이 그리기 실패", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("TemplateEditView - DrawOverlays 실패", ex);
            }
        }

        private void ImageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var position = e.GetPosition(ImageCanvas);
                var canvasSize = new Size(ImageCanvas.ActualWidth, ImageCanvas.ActualHeight);

                ViewModel.OnCanvasClick(position, canvasSize);
                DrawOverlays();
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("TemplateEditView - ImageCanvas_MouseLeftButtonDown 실패", ex);
            }
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // 행 번호를 1부터 시작하도록 설정
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        /// <summary>
        /// SelectedOverlay가 변경될 때마다 이전 오버레이 구독 해제 및 새 오버레이 구독 등록
        /// </summary>
        private void ResubscribeOverlay(RectangleOverlay? next)
        {
            // 이전 오버레이 구독 해제
            if (_subscribedOverlay != null)
            {
                _subscribedOverlay.PropertyChanged -= _overlayChangedHandler;
            }

            _subscribedOverlay = next;

            // 새 오버레이 구독 등록
            if (_subscribedOverlay != null)
            {
                _subscribedOverlay.PropertyChanged += _overlayChangedHandler;
            }
        }

        /// <summary>
        /// 오버레이의 속성(X, Y, Width, Height) 변경 시 화면 갱신
        /// </summary>
        private void Overlay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 좌표/크기 변경만 반응
            if (e.PropertyName is nameof(RectangleOverlay.X)
                or nameof(RectangleOverlay.Y)
                or nameof(RectangleOverlay.Width)
                or nameof(RectangleOverlay.Height))
            {
                DrawOverlays();
            }
        }

        /// <summary>
        /// View가 언로드될 때 구독 해제하여 메모리 누수 방지
        /// </summary>
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            ResubscribeOverlay(null);
        }

        /// <summary>
        /// 숫자 입력 TextBox 포커스 시 전체 선택
        /// </summary>
        private void NumericTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectAll();
            }
        }

        /// <summary>
        /// 방향키로 오버레이 위치/크기 미세 조정
        /// Shift + 방향키: 크기 조정
        /// 일반 방향키: 위치 조정
        /// </summary>
        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ViewModel.SelectedOverlay == null) return;

            // TextBox에서 편집 중이면 기본 커서 이동을 존중
            if (Keyboard.FocusedElement is TextBox) return;

            var overlay = ViewModel.SelectedOverlay;
            var step = 1.0; // 기본 이동 단위

            // Shift가 눌려있으면 크기 조정 모드
            bool isResizeMode = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            switch (e.Key)
            {
                case Key.Left:
                    if (isResizeMode)
                    {
                        // 왼쪽으로 크기 조정 (X는 유지, Width만 감소)
                        overlay.Width = Math.Max(1.0, overlay.Width - step);
                    }
                    else
                    {
                        overlay.X -= step;
                    }
                    e.Handled = true;
                    break;

                case Key.Right:
                    if (isResizeMode)
                    {
                        // 오른쪽으로 크기 조정 (Width 증가)
                        overlay.Width += step;
                    }
                    else
                    {
                        overlay.X += step;
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (isResizeMode)
                    {
                        // 위로 크기 조정 (Y는 유지, Height만 감소)
                        overlay.Height = Math.Max(1.0, overlay.Height - step);
                    }
                    else
                    {
                        overlay.Y -= step;
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    if (isResizeMode)
                    {
                        // 아래로 크기 조정 (Height 증가)
                        overlay.Height += step;
                    }
                    else
                    {
                        overlay.Y += step;
                    }
                    e.Handled = true;
                    break;
            }
        }
    }
}










