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
    public partial class MainWindow : Window
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;
        private bool _isUpdatingCanvas = false;  // Canvas 업데이트 중 플래그 (무한 루프 방지)
        
        // 이벤트 핸들러 구독 관리 (누적 방지)
        private Models.ImageDocument? _currentSubscribedDocument;
        private NotifyCollectionChangedEventHandler? _overlaysCollectionChangedHandler;
        private PropertyChangedEventHandler? _documentPropertyChangedHandler;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = new MainViewModel();
                
                // 이미지 문서 변경 감지
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                if (ViewModel.Workspace != null)
                {
                    ViewModel.Workspace.PropertyChanged += Workspace_PropertyChanged;
                    ViewModel.Workspace.Documents.CollectionChanged += (s, e) =>
                    {
                        UpdateImageDisplay();
                        DrawOverlays();
                    };
                }

                // SelectedDocument의 Overlays 변경 감지
                if (ViewModel.SelectedDocument != null)
                {
                    ViewModel.SelectedDocument.Overlays.CollectionChanged += (s, e) => DrawOverlays();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"창 초기화 중 오류가 발생했습니다:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "초기화 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                Logger.Instance.Debug($"ViewModel_PropertyChanged: {e.PropertyName}");
                
                if (e.PropertyName == nameof(ViewModel.SelectedDocument))
                {
                    Logger.Instance.Debug($"SelectedDocument 변경. UpdateImageDisplay 호출");
                    
                    // 이전 문서의 이벤트 핸들러 해제 (누적 방지)
                    if (_currentSubscribedDocument != null)
                    {
                        if (_overlaysCollectionChangedHandler != null)
                        {
                            _currentSubscribedDocument.Overlays.CollectionChanged -= _overlaysCollectionChangedHandler;
                            _overlaysCollectionChangedHandler = null;
                            Logger.Instance.Debug($"이전 문서의 Overlays.CollectionChanged 핸들러 해제");
                        }
                        
                        if (_documentPropertyChangedHandler != null)
                        {
                            _currentSubscribedDocument.PropertyChanged -= _documentPropertyChangedHandler;
                            _documentPropertyChangedHandler = null;
                            Logger.Instance.Debug($"이전 문서의 PropertyChanged 핸들러 해제");
                        }
                    }
                    
                    UpdateImageDisplay();
                    Logger.Instance.Debug($"DrawOverlays 호출");
                    DrawOverlays();

                    // 새 문서의 이벤트 핸들러 등록
                    if (ViewModel.SelectedDocument != null)
                    {
                        _currentSubscribedDocument = ViewModel.SelectedDocument;
                        
                        Logger.Instance.Debug($"SelectedDocument의 Overlays 변경 감지 등록");
                        _overlaysCollectionChangedHandler = (s, args) => 
                        {
                            try
                            {
                                DrawOverlays();
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error("Overlays.CollectionChanged에서 DrawOverlays 실패", ex);
                            }
                        };
                        ViewModel.SelectedDocument.Overlays.CollectionChanged += _overlaysCollectionChangedHandler;
                        
                        _documentPropertyChangedHandler = (s, args) =>
                        {
                            try
                            {
                                if (args.PropertyName == nameof(ViewModel.SelectedDocument.ImageWidth) ||
                                    args.PropertyName == nameof(ViewModel.SelectedDocument.ImageHeight))
                                {
                                    UpdateImageDisplay();
                                    DrawOverlays();
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Instance.Error("SelectedDocument.PropertyChanged에서 업데이트 실패", ex);
                            }
                        };
                        ViewModel.SelectedDocument.PropertyChanged += _documentPropertyChangedHandler;
                    }
                    else
                    {
                        _currentSubscribedDocument = null;
                    }
                }
                
                // CurrentImageDisplayRect 변경 시에는 DrawOverlays만 호출 (UpdateImageDisplay는 호출하지 않음)
                // UpdateImageDisplay 내부에서 UpdateImageDisplayRect를 호출하므로 무한 루프 방지
                if (e.PropertyName == nameof(ViewModel.CurrentImageDisplayRect))
                {
                    Logger.Instance.Debug($"CurrentImageDisplayRect 변경. DrawOverlays만 호출");
                    DrawOverlays();
                }

                if (e.PropertyName == nameof(ViewModel.SelectedOverlay))
                {
                    Logger.Instance.Debug($"SelectedOverlay 변경. DrawOverlays 호출");
                    DrawOverlays();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"ViewModel_PropertyChanged에서 예외 발생. PropertyName: {e.PropertyName}", ex);
                MessageBox.Show($"UI 업데이트 중 오류가 발생했습니다:\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Workspace_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // SelectedDocumentId 변경은 ViewModel.SelectedDocument 변경으로 처리되므로
            // 여기서는 UpdateImageDisplay를 호출하지 않음 (중복 호출 방지)
            // ViewModel.SelectedDocument 변경 시 ViewModel_PropertyChanged에서 처리됨
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateImageDisplay();
            DrawOverlays();
        }

        private void ImageCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Canvas 업데이트 중이면 무시 (무한 루프 방지)
            if (_isUpdatingCanvas) return;
            
            UpdateImageDisplay();
            DrawOverlays();
        }

        private void UpdateImageDisplay()
        {
            try
            {
                Logger.Instance.Debug($"UpdateImageDisplay 시작. SelectedDocument: {(ViewModel.SelectedDocument != null ? ViewModel.SelectedDocument.SourcePath : "null")}");
                
                if (ViewModel.SelectedDocument == null)
                {
                    Logger.Instance.Debug("SelectedDocument가 null이므로 SourceImage.Source를 null로 설정");
                    SourceImage.Source = null;
                    return;
                }

                var imagePath = ViewModel.SelectedDocument.SourcePath;
                Logger.Instance.Debug($"이미지 경로: {imagePath}, 파일 존재: {File.Exists(imagePath)}");
                
                if (File.Exists(imagePath))
                {
                    Logger.Instance.Debug("BitmapImage 생성 시작");
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    SourceImage.Source = bitmap;
                    Logger.Instance.Debug($"BitmapImage 생성 완료. 크기: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
                }

                // 이미지 표시 영역 계산
                // 주의: ImageCanvas.ActualWidth/Height를 사용하지 않음 (Canvas 크기 변경 시 무한 루프 발생)
                // ScrollViewer의 ViewportWidth/Height를 사용
                var viewportWidth = ImageScrollViewer.ViewportWidth;
                var viewportHeight = ImageScrollViewer.ViewportHeight;
                
                Logger.Instance.Debug($"뷰포트 크기: {viewportWidth}x{viewportHeight}");
                
                if (viewportWidth <= 0 || viewportHeight <= 0)
                {
                    Logger.Instance.Warning($"뷰포트 크기가 유효하지 않음: {viewportWidth}x{viewportHeight}. 업데이트 스킵");
                    return;
                }
                
                var availableSize = new System.Windows.Size(viewportWidth, viewportHeight);

                // ViewModel의 UpdateImageDisplayRect 사용 (왼쪽 위 정렬)
                ViewModel.UpdateImageDisplayRect(availableSize);
                var displayRect = ViewModel.CurrentImageDisplayRect;
                
                Logger.Instance.Debug($"표시 영역 계산 완료: X={displayRect.X}, Y={displayRect.Y}, W={displayRect.Width}, H={displayRect.Height}");

                // Canvas 크기: 이미지 전체를 포함하도록 설정 (잘림 방지)
                // 가로폭: 여유를 조금 추가
                // 세로폭: 이미지 높이에 맞추되 최소한의 여유만
                const double horizontalPadding = 10;  // 가로 여유
                const double verticalPadding = 10;  // 세로 여유
                
                var requiredCanvasWidth = Math.Max(displayRect.Width + horizontalPadding, availableSize.Width);
                // A4 가로 이미지: 이미지 높이에 딱 맞춤 (하단 여백 최소화)
                // availableSize.Height와 비교하지 않아 뷰포트보다 작아도 됨
                var requiredCanvasHeight = displayRect.Height + verticalPadding;
                
                // Canvas 크기 변경 시 SizeChanged 이벤트가 발생하지 않도록 플래그 설정
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
                
                Logger.Instance.Debug($"Canvas 크기 설정: {requiredCanvasWidth}x{requiredCanvasHeight}");

                // Image 위치 및 크기 설정
                // 왼쪽 위 정렬이므로 X=0, Y=0
                Canvas.SetLeft(SourceImage, 0);
                Canvas.SetTop(SourceImage, 0);
                SourceImage.Width = displayRect.Width;
                SourceImage.Height = displayRect.Height;
                
                Logger.Instance.Debug("UpdateImageDisplay 완료");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("UpdateImageDisplay 실패", ex);
                MessageBox.Show($"이미지 표시 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DrawOverlays()
        {
            try
            {
                Logger.Instance.Debug("DrawOverlays 시작");
                
                // 기존 오버레이 제거
                var overlaysToRemove = ImageCanvas.Children.OfType<Rectangle>().ToList();
                Logger.Instance.Debug($"제거할 오버레이 수: {overlaysToRemove.Count}");
                foreach (var rect in overlaysToRemove)
                {
                    ImageCanvas.Children.Remove(rect);
                }

                if (ViewModel.SelectedDocument == null)
                {
                    Logger.Instance.Debug("SelectedDocument가 null이므로 DrawOverlays 종료");
                    return;
                }

                var displayRect = ViewModel.CurrentImageDisplayRect;
                var doc = ViewModel.SelectedDocument;
                
                Logger.Instance.Debug($"표시 영역: {displayRect.Width}x{displayRect.Height}, 문서 크기: {doc.ImageWidth}x{doc.ImageHeight}, 오버레이 수: {doc.Overlays.Count}");

                // 0으로 나누기 방지
                if (doc.ImageWidth <= 0 || doc.ImageHeight <= 0)
                {
                    Logger.Instance.Warning($"문서 크기가 유효하지 않음: {doc.ImageWidth}x{doc.ImageHeight}. DrawOverlays 스킵");
                    return;
                }
                
                if (displayRect.Width <= 0 || displayRect.Height <= 0)
                {
                    Logger.Instance.Warning($"표시 영역이 유효하지 않음: {displayRect.Width}x{displayRect.Height}. DrawOverlays 스킵");
                    return;
                }

                // 스케일 계산 (Uniform이므로 가로/세로 동일)
                var scaleX = displayRect.Width / doc.ImageWidth;
                var scaleY = displayRect.Height / doc.ImageHeight;
                
                Logger.Instance.Debug($"스케일 계산: scaleX={scaleX}, scaleY={scaleY}");

                foreach (var overlay in doc.Overlays)
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

                        ImageCanvas.Children.Add(rect);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error($"오버레이 그리기 실패. X={overlay.X}, Y={overlay.Y}, W={overlay.Width}, H={overlay.Height}", ex);
                    }
                }
                
                Logger.Instance.Debug($"DrawOverlays 완료. 추가된 오버레이 수: {doc.Overlays.Count}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("DrawOverlays 실패", ex);
            }
        }

        private void ImageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Logger.Instance.Debug($"ImageCanvas_MouseLeftButtonDown 발생. IsAddMode: {ViewModel.IsAddMode}");
                
                var position = e.GetPosition(ImageCanvas);
                var canvasSize = new System.Windows.Size(ImageCanvas.ActualWidth, ImageCanvas.ActualHeight);
                
                Logger.Instance.Debug($"클릭 위치: ({position.X}, {position.Y}), Canvas 크기: {canvasSize.Width}x{canvasSize.Height}");

                ViewModel.OnCanvasClick(position, canvasSize);
                DrawOverlays();
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("ImageCanvas_MouseLeftButtonDown 실패", ex);
                MessageBox.Show($"클릭 처리 중 오류가 발생했습니다:\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 종료 시 자동 저장 (선택사항)
            // ViewModel.SaveCommand.Execute(null);
            base.OnClosed(e);
        }
    }
}

