
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

        // 드래그 선택 관련
        private Point? _dragStartPoint;
        private Rectangle? _selectionBox;
        private bool _isSyncingDataGrid = false; // DataGrid 동기화 중 무한 루프 방지

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
                
                // SelectionVM 변경 감지
                ViewModel.SelectionVM.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(OverlaySelectionViewModel.Selected))
                    {
                        SyncDataGridSelection();
                        DrawOverlays();
                    }
                };
                
                if (ViewModel.Workspace?.Template != null)
                {
                    ViewModel.Workspace.Template.TimingMarks.CollectionChanged += (s, args) => DrawOverlays();
                    ViewModel.Workspace.Template.ScoringAreas.CollectionChanged += (s, args) => DrawOverlays();
                    ViewModel.Workspace.Template.BarcodeAreas.CollectionChanged += (s, args) => DrawOverlays();
                }

                // 초기 로드 시 안내 메시지 상태 업데이트
                UpdateNoImageHintVisibility();
                
                // 초기 커서 설정
                UpdateCursor();
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
                    // ✅ DrawOverlays는 SelectionVM.PropertyChanged에서 이미 호출됨
                }

                if (e.PropertyName == nameof(ViewModel.SelectionVM) ||
                    e.PropertyName == nameof(ViewModel.DisplayOverlays) ||
                    e.PropertyName == nameof(ViewModel.CurrentOverlayCollection) ||
                    e.PropertyName == nameof(ViewModel.CurrentOverlayType))
                {
                    DrawOverlays();
                }

                if (e.PropertyName == nameof(ViewModel.IsAddMode))
                {
                    UpdateCursor();
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

                // 선택된 오버레이를 리스트로 먼저 복사하여 열거 중 수정 방지
                var selectedOverlays = ViewModel.SelectionVM.Selected.ToList();

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

                        // 선택된 오버레이 강조 (다중 선택 지원)
                        bool isSelected = selectedOverlays.Contains(overlay);
                        if (isSelected)
                        {
                            rect.Stroke = Brushes.Blue;
                            rect.StrokeThickness = (overlay.StrokeThickness + 2) * Math.Min(scaleX, scaleY);
                        }

                        // 오버레이 타입에 따라 색상 구분
                        if (overlay.OverlayType == Models.OverlayType.TimingMark)
                        {
                            if (!isSelected)
                            {
                                rect.Stroke = Brushes.Green;
                            }
                        }
                        else if (overlay.OverlayType == Models.OverlayType.ScoringArea)
                        {
                            if (!isSelected)
                            {
                                // 선택된 문항(CurrentQuestionNumber)에 속한 오버레이는 한 색상, 그 외는 다른 색상
                                var question = ViewModel.Workspace.Template.Questions
                                    .FirstOrDefault(q => q.Options.Contains(overlay));

                                if (question != null && ViewModel.CurrentQuestionNumber.HasValue)
                                {
                                    // 선택된 문항에 속한 오버레이는 Red, 그 외는 Gray
                                    if (question.QuestionNumber == ViewModel.CurrentQuestionNumber.Value)
                                    {
                                        rect.Stroke = Brushes.Red;
                                    }
                                    else
                                    {
                                        rect.Stroke = Brushes.Gray;
                                    }
                                }
                                else
                                {
                                    rect.Stroke = Brushes.Gray;
                                }
                            }
                        }
                        else if (overlay.OverlayType == Models.OverlayType.BarcodeArea)
                        {
                            if (!isSelected)
                            {
                                rect.Stroke = Brushes.Orange;
                            }
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

                // 클릭한 오버레이 찾기
                var clickedOverlay = FindOverlayAtPosition(position, canvasSize);

                if (clickedOverlay != null && ViewModel.IsOverlaySelectable(clickedOverlay))
                {
                    bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                    bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                    if (!isCtrlPressed && !isShiftPressed)
                    {
                        // 단일 선택
                        ViewModel.SelectionVM.SetSelection(new[] { clickedOverlay });
                    }
                    else if (isCtrlPressed)
                    {
                        // Ctrl: 토글 선택
                        if (ViewModel.SelectionVM.Selected.Contains(clickedOverlay))
                        {
                            ViewModel.SelectionVM.Remove(clickedOverlay);
                        }
                        else
                        {
                            ViewModel.SelectionVM.Add(clickedOverlay);
                        }
                    }
                    else if (isShiftPressed)
                    {
                        // Shift: 범위 선택 (첫 번째 선택부터 현재까지)
                        var currentCollection = ViewModel.CurrentOverlayCollection;
                        if (currentCollection != null && ViewModel.SelectionVM.Selected.Count > 0)
                        {
                            var firstSelected = ViewModel.SelectionVM.Selected.First();
                            var firstIndex = currentCollection.IndexOf(firstSelected);
                            var clickedIndex = currentCollection.IndexOf(clickedOverlay);

                            if (firstIndex >= 0 && clickedIndex >= 0)
                            {
                                var startIndex = Math.Min(firstIndex, clickedIndex);
                                var endIndex = Math.Max(firstIndex, clickedIndex);
                                var range = currentCollection.Skip(startIndex).Take(endIndex - startIndex + 1)
                                    .Where(o => ViewModel.IsOverlaySelectable(o))
                                    .ToList();
                                ViewModel.SelectionVM.SetSelection(range);
                            }
                        }
                        else
                        {
                            ViewModel.SelectionVM.SetSelection(new[] { clickedOverlay });
                        }
                    }

                    // 추가 모드 해제
                    if (ViewModel.IsAddMode)
                    {
                        ViewModel.IsAddMode = false;
                    }
                }
                else
                {
                    // 빈 공간 클릭: 드래그 시작 또는 추가 모드 체크
                    if (ViewModel.IsAddMode)
                    {
                        ViewModel.OnCanvasClick(position, canvasSize);
                    }
                    else if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.LeftShift))
                    {
                        // Ctrl/Shift 없이 빈 공간 클릭 시 선택 해제
                        ViewModel.SelectionVM.Clear();
                    }

                    // 드래그 시작
                    _dragStartPoint = position;
                    ImageCanvas.CaptureMouse();
                }

                DrawOverlays();
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("TemplateEditView - ImageCanvas_MouseLeftButtonDown 실패", ex);
            }
        }

        private void ImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // 커서 업데이트 (추가 모드 여부에 따라)
            UpdateCursor();
            
            if (_dragStartPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPos = e.GetPosition(ImageCanvas);

                // 선택 박스 그리기
                if (_selectionBox == null)
                {
                    _selectionBox = new Rectangle
                    {
                        Stroke = Brushes.Blue,
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection { 4, 4 },
                        Fill = new SolidColorBrush(Color.FromArgb(30, 0, 120, 255)) // 반투명 파란색 채우기
                    };
                    ImageCanvas.Children.Add(_selectionBox);
                }

                var left = Math.Min(_dragStartPoint.Value.X, currentPos.X);
                var top = Math.Min(_dragStartPoint.Value.Y, currentPos.Y);
                var width = Math.Abs(currentPos.X - _dragStartPoint.Value.X);
                var height = Math.Abs(currentPos.Y - _dragStartPoint.Value.Y);

                Canvas.SetLeft(_selectionBox, left);
                Canvas.SetTop(_selectionBox, top);
                _selectionBox.Width = width;
                _selectionBox.Height = height;

                // 박스 내 오버레이 선택
                SelectOverlaysInBox(new Rect(left, top, width, height));
            }
        }

        private void ImageCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            // 마우스가 캔버스를 떠날 때 커서 복원
            if (!ViewModel.IsAddMode)
            {
                ImageCanvas.Cursor = Cursors.Arrow;
            }
        }

        private void UpdateCursor()
        {
            if (ImageCanvas == null) return;

            if (ViewModel.IsAddMode)
            {
                // 추가 모드일 때는 십자 모양이 아닌 다른 커서 (예: Cross 또는 Hand)
                ImageCanvas.Cursor = Cursors.Hand; // 또는 다른 적절한 커서
            }
            else
            {
                // 일반 모드일 때는 십자 모양 (선택 모드)
                ImageCanvas.Cursor = Cursors.Cross;
            }
        }

        private void ImageCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_selectionBox != null)
            {
                ImageCanvas.Children.Remove(_selectionBox);
                _selectionBox = null;
            }

            _dragStartPoint = null;
            ImageCanvas.ReleaseMouseCapture();
            ImageCanvas.Cursor = Cursors.Arrow;
            
            // 드래그 선택 후 DataGrid 동기화
            SyncDataGridSelection();
            DrawOverlays();
        }

        private RectangleOverlay? FindOverlayAtPosition(Point position, Size canvasSize)
        {
            if (ViewModel.SelectedDocument == null || ViewModel.Workspace?.Template == null)
                return null;

            var displayRect = ViewModel.CurrentImageDisplayRect;
            var doc = ViewModel.SelectedDocument;
            var template = ViewModel.Workspace.Template;

            if (doc.ImageWidth <= 0 || doc.ImageHeight <= 0 ||
                displayRect.Width <= 0 || displayRect.Height <= 0)
            {
                return null;
            }

            var scaleX = displayRect.Width / doc.ImageWidth;
            var scaleY = displayRect.Height / doc.ImageHeight;

            // 화면 좌표를 픽셀 좌표로 변환
            var pixelX = (position.X - displayRect.X) / scaleX;
            var pixelY = (position.Y - displayRect.Y) / scaleY;

            var allOverlays = template.TimingMarks
                .Concat(template.ScoringAreas)
                .Concat(template.BarcodeAreas)
                .ToList();

            // 클릭 위치에 있는 오버레이 찾기 (역순으로 검색하여 위에 있는 것 우선)
            foreach (var overlay in allOverlays.Reverse<RectangleOverlay>())
            {
                if (pixelX >= overlay.X && pixelX <= overlay.X + overlay.Width &&
                    pixelY >= overlay.Y && pixelY <= overlay.Y + overlay.Height)
                {
                    return overlay;
                }
            }

            return null;
        }

        private void SelectOverlaysInBox(Rect box)
        {
            if (ViewModel.SelectedDocument == null || ViewModel.Workspace?.Template == null)
                return;

            var displayRect = ViewModel.CurrentImageDisplayRect;
            var doc = ViewModel.SelectedDocument;

            if (doc.ImageWidth <= 0 || doc.ImageHeight <= 0 ||
                displayRect.Width <= 0 || displayRect.Height <= 0)
            {
                return;
            }

            var scaleX = displayRect.Width / doc.ImageWidth;
            var scaleY = displayRect.Height / doc.ImageHeight;

            // 박스 좌표를 픽셀 좌표로 변환
            var pixelLeft = (box.Left - displayRect.X) / scaleX;
            var pixelTop = (box.Top - displayRect.Y) / scaleY;
            var pixelRight = (box.Right - displayRect.X) / scaleX;
            var pixelBottom = (box.Bottom - displayRect.Y) / scaleY;

            var pixelBox = new Rect(
                Math.Min(pixelLeft, pixelRight),
                Math.Min(pixelTop, pixelBottom),
                Math.Abs(pixelRight - pixelLeft),
                Math.Abs(pixelBottom - pixelTop));

            var template = ViewModel.Workspace.Template;
            var allOverlays = template.TimingMarks
                .Concat(template.ScoringAreas)
                .Concat(template.BarcodeAreas)
                .ToList();

            // 박스와 겹치는 선택 가능한 오버레이 찾기
            var overlaysInBox = allOverlays
                .Where(o =>
                {
                    var overlayRect = new Rect(o.X, o.Y, o.Width, o.Height);
                    return pixelBox.IntersectsWith(overlayRect) && ViewModel.IsOverlaySelectable(o);
                })
                .ToList();

            ViewModel.SelectionVM.SetSelection(overlaysInBox);
            // 드래그 중에는 DataGrid 동기화하지 않음 (MouseLeftButtonUp에서 처리)
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // 행 번호를 1부터 시작하도록 설정
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void DataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isSyncingDataGrid) return; // 동기화 중이면 무시 (무한 루프 방지)
            
            if (sender is System.Windows.Controls.DataGrid dg)
            {
                // DataGrid 선택 변경 시 SelectionVM 동기화
                ViewModel.SyncSelectionFromDataGrid(dg.SelectedItems);
                DrawOverlays();
            }
        }

        private void SyncDataGridSelection()
        {
            if (OverlayDataGrid == null) return;
            if (_isSyncingDataGrid) return; // 이미 동기화 중이면 무시

            try
            {
                _isSyncingDataGrid = true;
                
                // SelectionVM 변경 시 DataGrid 선택 동기화
                // 먼저 리스트로 복사하여 열거 중 수정 방지
                var selectedOverlays = ViewModel.SelectionVM.Selected.ToList();
                
                OverlayDataGrid.SelectedItems.Clear();
                foreach (var overlay in selectedOverlays)
                {
                    OverlayDataGrid.SelectedItems.Add(overlay);
                }
            }
            finally
            {
                _isSyncingDataGrid = false;
            }
        }

        private void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyTextBoxValue(sender);
        }

        private void NumericTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // 값 적용 (모델 프로퍼티 변경)
                ApplyTextBoxValue(sender);
                
                // 포커스를 텍스트 박스에서 벗어나게 이동
                if (sender is TextBox tb)
                {
                    // UserControl로 포커스 이동 (포커스 가능한 요소)
                    this.Focus();
                    
                    // 이벤트 처리 완료 표시
                    e.Handled = true;
                }
                
                // ✅ DrawOverlays() 호출 불필요!
                // ApplyTextBoxValue → RectangleOverlay.X/Y/Width/Height 변경
                // → OverlaySelectionViewModel.Overlay_PropertyChanged 발생
                // → 오버레이의 PropertyChanged가 직접 발생
                // → DrawOverlays()는 오버레이 속성 변경 시 자동 호출됨
            }
        }

        private void ApplyTextBoxValue(object sender)
        {
            if (sender is TextBox tb)
            {
                // "다중 선택" 문자열을 파싱하여 숫자로 변환
                if (double.TryParse(tb.Text, out double value))
                {
                    // TextBox 이름에 따라 해당 속성에 값 설정
                    if (tb.Name == "XTextBox")
                    {
                        ViewModel.SelectionVM.X = value;
                    }
                    else if (tb.Name == "YTextBox")
                    {
                        ViewModel.SelectionVM.Y = value;
                    }
                    else if (tb.Name == "WidthTextBox")
                    {
                        ViewModel.SelectionVM.Width = value;
                    }
                    else if (tb.Name == "HeightTextBox")
                    {
                        ViewModel.SelectionVM.Height = value;
                    }
                }
            }
        }

        /// <summary>
        /// Ctrl + 마우스 휠로 줌 제어
        /// </summary>
        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Ctrl 키가 눌려있을 때만 줌 작동
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                return;
            }

            // 기본 스크롤 동작 방지
            e.Handled = true;

            if (ViewModel.SelectedDocument == null)
            {
                return;
            }

            // 줌 증감량 (휠 위로 = 줌 인, 아래로 = 줌 아웃)
            const double zoomFactor = 0.1; // 10%씩 증감
            double zoomDelta = e.Delta > 0 ? zoomFactor : -zoomFactor;

            // 현재 줌 레벨 계산
            double oldZoom = ViewModel.ZoomLevel;
            double newZoom = oldZoom + zoomDelta;

            // 마우스 위치를 중심으로 줌 (스크롤 위치 조정)
            var mousePosition = e.GetPosition(ImageScrollViewer);
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                // 스크롤 위치 계산
                double scrollX = scrollViewer.HorizontalOffset;
                double scrollY = scrollViewer.VerticalOffset;

                // 줌 레벨 변경
                ViewModel.ZoomLevel = newZoom;

                // 이미지 표시 영역 업데이트
                var viewportWidth = ImageScrollViewer.ViewportWidth;
                var viewportHeight = ImageScrollViewer.ViewportHeight;
                if (viewportWidth > 0 && viewportHeight > 0)
                {
                    var availableSize = new Size(viewportWidth, viewportHeight);
                    ViewModel.UpdateImageDisplayRect(availableSize);
                    UpdateImageDisplay();

                    // 마우스 위치를 중심으로 스크롤 조정
                    double zoomRatio = newZoom / oldZoom;
                    double newScrollX = (scrollX + mousePosition.X) * zoomRatio - mousePosition.X;
                    double newScrollY = (scrollY + mousePosition.Y) * zoomRatio - mousePosition.Y;

                    scrollViewer.ScrollToHorizontalOffset(Math.Max(0, newScrollX));
                    scrollViewer.ScrollToVerticalOffset(Math.Max(0, newScrollY));
                }
            }
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
        /// 다중 선택 지원
        /// </summary>
        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Z는 UndoCommand로 처리되므로 여기서는 무시
            if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                return;
            }

            if (ViewModel.SelectionVM.IsEmpty) return;

            // TextBox에서 편집 중이면 기본 커서 이동을 존중
            if (Keyboard.FocusedElement is TextBox) return;

            var step = 1.0; // 기본 이동 단위

            // Shift가 눌려있으면 크기 조정 모드
            bool isResizeMode = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            // 선택된 모든 오버레이에 적용
            foreach (var overlay in ViewModel.SelectionVM.Selected)
            {
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
                        break;
                }
            }

            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
            }
        }
    }
}










