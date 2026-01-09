using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SimpleOverlayEditor.ViewModels;
using SimpleOverlayEditor.Utils;
using SimpleOverlayEditor.Services;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// MarkingView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MarkingView : UserControl
    {
        private MarkingViewModel ViewModel => (MarkingViewModel)DataContext;
        private bool _isUpdatingCanvas = false;

        public MarkingView()
        {
            InitializeComponent();
            this.Loaded += MarkingView_Loaded;
        }

        private void MarkingView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(ViewModel.DisplayImage) || 
                    e.PropertyName == nameof(ViewModel.SelectedDocument))
                {
                    UpdateImageDisplay();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"MarkingView - ViewModel_PropertyChanged에서 예외 발생", ex);
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
                    // DisplayImage가 null이면 Source를 null로 설정하고 Canvas 크기를 최소화
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

                var doc = ViewModel.SelectedDocument;
                var displayImage = ViewModel.DisplayImage;

                // Image Source 설정
                SourceImage.Source = displayImage;

                // 이미지 표시 영역 계산
                var viewportWidth = ImageScrollViewer.ViewportWidth;
                var viewportHeight = ImageScrollViewer.ViewportHeight;

                if (viewportWidth <= 0 || viewportHeight <= 0)
                {
                    return;
                }

                var availableSize = new Size(viewportWidth, viewportHeight);
                var displayRect = ZoomHelper.CalculateImageDisplayRect(
                    doc.ImageWidth,
                    doc.ImageHeight,
                    availableSize,
                    ZoomHelper.ImageAlignment.TopLeft);

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
                Logger.Instance.Error("MarkingView - UpdateImageDisplay 실패", ex);
            }
        }
    }
}

