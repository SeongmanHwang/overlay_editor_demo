using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SimpleOverlayEditor.Models;
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

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem is OmrSheetResult sheetResult)
            {
                if (DataContext is MarkingViewModel viewModel)
                {
                    viewModel.SelectDocumentByImageId(sheetResult.ImageId);
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string imageId)
            {
                if (DataContext is MarkingViewModel viewModel)
                {
                    viewModel.DeleteSingleItem(imageId);
                }
            }
        }

        private void OmrDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (ViewModel?.FilteredSheetResults == null) return;

            var grid = (DataGrid)sender;
            var view = ViewModel.FilteredSheetResults;

            e.Handled = true; // 기본 정렬 막기

            var key = e.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(key)) return;

            // 현재 정렬 목록을 복사
            var current = view.SortDescriptions.ToList();

            // 클릭한 열의 위치 찾기
            var existingIndex = current.FindIndex(sd => sd.PropertyName == key);
            bool isPrimary = existingIndex == 0;  // 0이면 1순위
            bool existsSomewhere = existingIndex >= 0;

            ListSortDirection newDir;

            if (isPrimary)
            {
                // 1순위 열을 다시 클릭: 토글
                newDir = current[0].Direction == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else if (existsSomewhere)
            {
                // 2순위 이하로 있던 열을 1순위로 올림: 방향 유지
                newDir = current[existingIndex].Direction;
            }
            else
            {
                // 처음 등장한 열: 항상 오름차순
                newDir = ListSortDirection.Ascending;
            }

            // 클릭한 열을 제거한 나머지(방향 포함 그대로 유지)
            var rest = current.Where(sd => sd.PropertyName != key).ToList();

            // 클릭한 열을 1순위로 올리고, 기존 정렬들은 차순위로 유지
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(key, newDir));
            foreach (var sd in rest)
                view.SortDescriptions.Add(sd);

            view.Refresh();

            // UI 아이콘은 1순위만 표시
            foreach (var col in grid.Columns)
                col.SortDirection = null;

            e.Column.SortDirection = newDir;
        }
    }
}

