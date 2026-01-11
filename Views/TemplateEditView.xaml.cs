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

        public TemplateEditView()
        {
            InitializeComponent();

            // ViewModel이 설정된 후 이벤트 구독
            this.Loaded += TemplateEditView_Loaded;
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
                }

                if (e.PropertyName == nameof(ViewModel.CurrentImageDisplayRect))
                {
                    DrawOverlays();
                }

                if (e.PropertyName == nameof(ViewModel.SelectedOverlay) ||
                    e.PropertyName == nameof(ViewModel.DisplayOverlays) ||
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

        private void UpdateImageDisplay()
        {
            try
            {
                if (ViewModel.SelectedDocument == null)
                {
                    SourceImage.Source = null;
                    return;
                }

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
    }
}










