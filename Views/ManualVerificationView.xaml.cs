using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.ViewModels;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// ManualVerificationView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ManualVerificationView : UserControl
    {
        private ManualVerificationViewModel ViewModel => (ManualVerificationViewModel)DataContext;
        private bool _isUpdatingCanvas = false;

        public ManualVerificationView()
        {
            InitializeComponent();
            Loaded += ManualVerificationView_Loaded;
        }

        private void ManualVerificationView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdateImageDisplay();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(ViewModel.DisplayImage) ||
                    e.PropertyName == nameof(ViewModel.SelectedDocument) ||
                    e.PropertyName == nameof(ViewModel.CurrentImageDisplayRect))
                {
                    UpdateImageDisplay();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("ManualVerificationView - ViewModel_PropertyChanged 실패", ex);
            }
        }

        private void ImageCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isUpdatingCanvas) return;
            UpdateImageDisplay();
        }

        private void UpdateImageDisplay()
        {
            try
            {
                if (ViewModel?.SelectedDocument == null || ViewModel.DisplayImage == null)
                {
                    SourceImage.Source = null;
                    _isUpdatingCanvas = true;
                    try
                    {
                        ImageCanvas.Width = 0;
                        ImageCanvas.Height = 0;
                    }
                    finally
                    {
                        _isUpdatingCanvas = false;
                    }
                    return;
                }

                SourceImage.Source = ViewModel.DisplayImage;

                var viewportWidth = ImageScrollViewer.ViewportWidth;
                var viewportHeight = ImageScrollViewer.ViewportHeight;
                if (viewportWidth <= 0 || viewportHeight <= 0) return;

                ViewModel.UpdateImageDisplayRect(new Size(viewportWidth, viewportHeight));
                var displayRect = ViewModel.CurrentImageDisplayRect;

                const double horizontalPadding = 10;
                const double verticalPadding = 10;

                var requiredCanvasWidth = Math.Max(displayRect.Width + horizontalPadding, viewportWidth);
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

                Canvas.SetLeft(SourceImage, 0);
                Canvas.SetTop(SourceImage, 0);
                SourceImage.Width = displayRect.Width;
                SourceImage.Height = displayRect.Height;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("ManualVerificationView - UpdateImageDisplay 실패", ex);
            }
        }

        /// <summary>
        /// Ctrl + 마우스 휠로 줌 제어
        /// </summary>
        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            e.Handled = true;

            if (ViewModel.SelectedDocument == null) return;

            const double zoomFactor = 0.1;
            double zoomDelta = e.Delta > 0 ? zoomFactor : -zoomFactor;

            double oldZoom = ViewModel.ZoomLevel;
            double newZoom = oldZoom + zoomDelta;

            var mousePosition = e.GetPosition(ImageScrollViewer);
            if (sender is ScrollViewer scrollViewer)
            {
                double scrollX = scrollViewer.HorizontalOffset;
                double scrollY = scrollViewer.VerticalOffset;

                ViewModel.ZoomLevel = newZoom;

                var viewportWidth = ImageScrollViewer.ViewportWidth;
                var viewportHeight = ImageScrollViewer.ViewportHeight;
                if (viewportWidth > 0 && viewportHeight > 0)
                {
                    ViewModel.UpdateImageDisplayRect(new Size(viewportWidth, viewportHeight));
                    UpdateImageDisplay();

                    double zoomRatio = ViewModel.ZoomLevel / oldZoom;
                    double newScrollX = (scrollX + mousePosition.X) * zoomRatio - mousePosition.X;
                    double newScrollY = (scrollY + mousePosition.Y) * zoomRatio - mousePosition.Y;

                    scrollViewer.ScrollToHorizontalOffset(Math.Max(0, newScrollX));
                    scrollViewer.ScrollToVerticalOffset(Math.Max(0, newScrollY));
                }
            }
        }
    }
}
