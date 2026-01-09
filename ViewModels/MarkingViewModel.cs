using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 마킹 감지 전용 ViewModel입니다.
    /// </summary>
    public class MarkingViewModel : INotifyPropertyChanged
    {
        private readonly MarkingDetector _markingDetector;
        private readonly NavigationViewModel _navigation;
        private readonly Workspace _workspace;
        private readonly StateStore _stateStore;
        private readonly ImageLoader _imageLoader;
        private readonly ImageAlignmentService _alignmentService;
        private readonly Renderer _renderer;
        private List<MarkingResult>? _currentMarkingResults;
        private double _markingThreshold = 180.0;
        private BitmapSource? _displayImage;

        public MarkingViewModel(MarkingDetector markingDetector, NavigationViewModel navigation, Workspace workspace, StateStore stateStore)
        {
            _markingDetector = markingDetector ?? throw new ArgumentNullException(nameof(markingDetector));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _imageLoader = new ImageLoader();
            _alignmentService = new ImageAlignmentService();
            _renderer = new Renderer();

            DetectMarkingsCommand = new RelayCommand(
                OnDetectMarkings, 
                () => SelectedDocument != null && ScoringAreas != null && ScoringAreas.Count() > 0);
            DetectAllMarkingsCommand = new RelayCommand(
                OnDetectAllMarkings, 
                () => Documents != null && Documents.Count() > 0 && ScoringAreas != null && ScoringAreas.Count() > 0);
            LoadFolderCommand = new RelayCommand(OnLoadFolder);
        }

        public ImageDocument? SelectedDocument 
        { 
            get => _selectedDocument;
            set
            {
                if (!ReferenceEquals(_selectedDocument, value))
                {
                    _selectedDocument = value;
                    
                    // Workspace와 동기화
                    _workspace.SelectedDocumentId = value?.ImageId;
                    
                    // 문서 변경 시 마킹 결과 복원 또는 초기화
                    if (_selectedDocument == null)
                    {
                        CurrentMarkingResults = null;
                        DisplayImage = null;
                    }
                    else
                    {
                        // Workspace.MarkingResults에서 해당 문서의 마킹 결과 복원
                        if (_workspace.MarkingResults != null && 
                            _workspace.MarkingResults.TryGetValue(_selectedDocument.ImageId, out var results))
                        {
                            CurrentMarkingResults = results;
                        }
                        else
                        {
                            // 마킹 결과가 없으면 null로 설정 (마킹 감지 전까지)
                            CurrentMarkingResults = null;
                            DisplayImage = null;
                        }
                    }
                    
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedDocument));
                    
                    // 마킹 결과가 있을 때만 DisplayImage 생성
                    if (CurrentMarkingResults != null)
                    {
                        UpdateDisplayImage();
                    }
                }
            }
        }
        private ImageDocument? _selectedDocument;

        /// <summary>
        /// 중앙 패널에 표시할 오버레이 이미지
        /// </summary>
        public BitmapSource? DisplayImage
        {
            get => _displayImage;
            private set
            {
                _displayImage = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ImageDocument>? Documents { get; set; }
        public IEnumerable<RectangleOverlay>? ScoringAreas { get; set; }

        /// <summary>
        /// 문서 개수 (UI 표시용)
        /// </summary>
        public int DocumentCount => Documents?.Count ?? 0;

        public List<MarkingResult>? CurrentMarkingResults
        {
            get => _currentMarkingResults;
            set
            {
                _currentMarkingResults = value;
                OnPropertyChanged();
                UpdateDisplayImage(); // 마킹 결과 변경 시 이미지 업데이트
            }
        }

        public double MarkingThreshold
        {
            get => _markingThreshold;
            set
            {
                _markingThreshold = value;
                OnPropertyChanged();
            }
        }

        public ICommand DetectMarkingsCommand { get; }
        public ICommand DetectAllMarkingsCommand { get; }
        public ICommand LoadFolderCommand { get; }
        
        /// <summary>
        /// 네비게이션 ViewModel (홈으로 이동 등)
        /// </summary>
        public NavigationViewModel Navigation => _navigation;

        /// <summary>
        /// 현재 선택된 문서의 마킹을 감지합니다.
        /// </summary>
        private void OnDetectMarkings()
        {
            if (SelectedDocument == null)
            {
                MessageBox.Show("이미지를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ScoringAreas == null || !ScoringAreas.Any())
            {
                MessageBox.Show("채점 영역(ScoringArea)이 없습니다.\n먼저 채점 영역을 추가해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Logger.Instance.Info($"마킹 감지 시작: {SelectedDocument.SourcePath}");
                
                var results = _markingDetector.DetectMarkings(
                    SelectedDocument, 
                    ScoringAreas, 
                    MarkingThreshold);

                CurrentMarkingResults = results;

                // Workspace에 마킹 결과 저장
                if (SelectedDocument != null)
                {
                    if (_workspace.MarkingResults == null)
                    {
                        _workspace.MarkingResults = new Dictionary<string, List<MarkingResult>>();
                    }
                    _workspace.MarkingResults[SelectedDocument.ImageId] = results;
                }

                // 오버레이 이미지 생성 및 표시
                UpdateDisplayImage();

                // 결과 이미지 파일 저장
                if (SelectedDocument != null)
                {
                    try
                    {
                        _renderer.RenderSingleDocument(SelectedDocument, _workspace);
                        Logger.Instance.Info($"결과 이미지 파일 저장 완료: {SelectedDocument.SourcePath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("결과 이미지 파일 저장 실패", ex);
                        // 저장 실패해도 마킹 감지는 완료되었으므로 계속 진행
                    }
                }

                var markedCount = results.Count(r => r.IsMarked);
                var message = $"마킹 감지 완료\n\n" +
                             $"총 영역: {results.Count}개\n" +
                             $"마킹 감지: {markedCount}개\n" +
                             $"미마킹: {results.Count - markedCount}개\n\n" +
                             $"임계값: {MarkingThreshold}\n\n" +
                             $"결과 이미지가 저장되었습니다.";

                Logger.Instance.Info($"마킹 감지 완료: {markedCount}/{results.Count}개 마킹 감지");
                MessageBox.Show(message, "마킹 감지 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("마킹 감지 실패", ex);
                MessageBox.Show($"마킹 감지 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모든 문서의 마킹을 감지합니다.
        /// </summary>
        private void OnDetectAllMarkings()
        {
            if (Documents == null || !Documents.Any())
            {
                MessageBox.Show("로드된 이미지가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ScoringAreas == null || !ScoringAreas.Any())
            {
                MessageBox.Show("채점 영역(ScoringArea)이 없습니다.\n먼저 채점 영역을 추가해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Workspace를 임시로 생성하여 DetectAllMarkings 호출
                var workspace = new Workspace
                {
                    Documents = new System.Collections.ObjectModel.ObservableCollection<ImageDocument>(Documents)
                };
                foreach (var area in ScoringAreas)
                {
                    workspace.Template.ScoringAreas.Add(area);
                }

                Logger.Instance.Info($"전체 문서 마킹 감지 시작: {Documents.Count()}개 문서");
                
                var allResults = _markingDetector.DetectAllMarkings(workspace, MarkingThreshold);

                // Workspace에 마킹 결과 저장
                _workspace.MarkingResults = allResults;

                int totalDocuments = allResults.Count;
                int totalAreas = 0;
                int totalMarked = 0;

                foreach (var kvp in allResults)
                {
                    totalAreas += kvp.Value.Count;
                    totalMarked += kvp.Value.Count(r => r.IsMarked);
                }

                var message = $"전체 문서 마킹 감지 완료\n\n" +
                             $"처리된 문서: {totalDocuments}개\n" +
                             $"총 영역: {totalAreas}개\n" +
                             $"마킹 감지: {totalMarked}개\n" +
                             $"미마킹: {totalAreas - totalMarked}개\n\n" +
                             $"임계값: {MarkingThreshold}";

                Logger.Instance.Info($"전체 문서 마킹 감지 완료: {totalMarked}/{totalAreas}개 마킹 감지");
                MessageBox.Show(message, "전체 마킹 감지 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                // 현재 선택된 문서의 결과 표시
                if (SelectedDocument != null && allResults.TryGetValue(SelectedDocument.ImageId, out var currentResults))
                {
                    CurrentMarkingResults = currentResults;
                    UpdateDisplayImage();
                }

                // 모든 문서의 결과 이미지 파일 저장
                try
                {
                    _renderer.RenderAll(_workspace);
                    Logger.Instance.Info($"전체 결과 이미지 파일 저장 완료");
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error("전체 결과 이미지 파일 저장 실패", ex);
                    // 저장 실패해도 마킹 감지는 완료되었으므로 계속 진행
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("전체 마킹 감지 실패", ex);
                MessageBox.Show($"전체 마킹 감지 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 폴더에서 이미지를 로드합니다.
        /// </summary>
        private void OnLoadFolder()
        {
            Logger.Instance.Info("폴더 로드 시작");
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "이미지가 있는 폴더를 선택하세요",
                    SelectedPath = _workspace.InputFolderPath
                };

                Logger.Instance.Debug($"FolderBrowserDialog 표시. 초기 경로: {_workspace.InputFolderPath}");
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var folderPath = dialog.SelectedPath;
                    Logger.Instance.Info($"선택된 폴더: {folderPath}");
                    
                    Logger.Instance.Debug("이미지 파일 로드 시작");
                    var documents = _imageLoader.LoadImagesFromFolder(folderPath);
                    Logger.Instance.Info($"이미지 파일 로드 완료. 문서 수: {documents.Count}");

                    if (documents.Count == 0)
                    {
                        Logger.Instance.Warning("선택한 폴더에 이미지 파일이 없음");
                        MessageBox.Show("선택한 폴더에 이미지 파일이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    Logger.Instance.Debug("SelectedDocument를 null로 설정 (Clear 전)");
                    SelectedDocument = null;
                    
                    _workspace.InputFolderPath = folderPath;
                    Logger.Instance.Debug($"Workspace.InputFolderPath 설정: {folderPath}");
                    
                    Logger.Instance.Debug("Workspace.Documents.Clear() 호출");
                    _workspace.Documents.Clear();
                    
                    Logger.Instance.Debug($"문서 {documents.Count}개 추가 및 정렬 적용 시작");
                    foreach (var doc in documents)
                    {
                        // 이미지 정렬 적용
                        ApplyAlignmentToDocument(doc);
                        
                        _workspace.Documents.Add(doc);
                        Logger.Instance.Debug($"문서 추가: {doc.SourcePath} (ID: {doc.ImageId}, 크기: {doc.ImageWidth}x{doc.ImageHeight})");
                    }
                    Logger.Instance.Debug("모든 문서 추가 완료");

                    // 첫 번째 문서 자동 선택하지 않음 (사용자가 직접 선택해야 함)
                    SelectedDocument = null;
                    _workspace.SelectedDocumentId = null;
                    CurrentMarkingResults = null;
                    DisplayImage = null;

                    // Documents 컬렉션 업데이트 (Workspace의 ObservableCollection을 직접 사용)
                    Documents = _workspace.Documents;
                    OnPropertyChanged(nameof(Documents));
                    OnPropertyChanged(nameof(DocumentCount));

                    // Workspace 저장
                    _stateStore.Save(_workspace);

                    Logger.Instance.Info($"폴더 로드 완료. 총 {documents.Count}개 이미지 로드됨");
                    MessageBox.Show($"{documents.Count}개의 이미지를 로드했습니다.", "로드 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Logger.Instance.Info("폴더 선택 취소됨");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("폴더 로드 실패", ex);
                MessageBox.Show($"폴더 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 이미지에 정렬을 적용하고 캐시에 저장합니다.
        /// </summary>
        private void ApplyAlignmentToDocument(ImageDocument document)
        {
            try
            {
                // 타이밍 마크가 없으면 정렬 생략
                if (_workspace.Template.TimingMarks.Count == 0)
                {
                    Logger.Instance.Debug($"타이밍 마크가 없어 정렬 생략: {document.SourcePath}");
                    return;
                }

                Logger.Instance.Debug($"이미지 정렬 적용 시작: {document.SourcePath}");

                // 원본 이미지 로드
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(document.SourcePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                // 정렬 적용
                var result = _alignmentService.AlignImage(bitmap, _workspace.Template);

                // 정렬 정보 저장
                document.AlignmentInfo = new AlignmentInfo
                {
                    Success = result.Success,
                    Confidence = result.Confidence
                };

                if (result.Success && result.Transform != null)
                {
                    document.AlignmentInfo.Rotation = result.Transform.Rotation;
                    document.AlignmentInfo.ScaleX = result.Transform.ScaleX;
                    document.AlignmentInfo.ScaleY = result.Transform.ScaleY;
                    document.AlignmentInfo.TranslationX = result.Transform.TranslationX;
                    document.AlignmentInfo.TranslationY = result.Transform.TranslationY;

                    // 정렬된 이미지를 캐시에 저장
                    var alignedImagePath = SaveAlignedImageToCache(document, result.AlignedImage);
                    document.AlignmentInfo.AlignedImagePath = alignedImagePath;

                    // 정렬된 이미지 크기로 ImageWidth/Height 업데이트
                    document.ImageWidth = result.AlignedImage.PixelWidth;
                    document.ImageHeight = result.AlignedImage.PixelHeight;

                    Logger.Instance.Info(
                        $"이미지 정렬 성공: {document.SourcePath}, " +
                        $"신뢰도={result.Confidence:F2}, " +
                        $"정렬된 이미지={alignedImagePath}");
                }
                else
                {
                    Logger.Instance.Info(
                        $"이미지 정렬 실패 또는 생략: {document.SourcePath}, " +
                        $"신뢰도={result.Confidence:F2}, 원본 이미지 사용");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"이미지 정렬 중 오류: {document.SourcePath}", ex);
                // 오류 발생 시 정렬 정보를 실패로 설정하고 원본 사용
                document.AlignmentInfo = new AlignmentInfo { Success = false, Confidence = 0.0 };
            }
        }

        /// <summary>
        /// 정렬된 이미지를 캐시 폴더에 저장합니다.
        /// </summary>
        private string SaveAlignedImageToCache(ImageDocument document, BitmapSource alignedImage)
        {
            try
            {
                PathService.EnsureDirectories();

                // 캐시 파일명 생성 (원본 파일명 + ImageId 해시)
                var originalFileName = Path.GetFileNameWithoutExtension(document.SourcePath);
                var cacheFileName = $"{originalFileName}_{document.ImageId.Substring(0, 8)}_aligned.png";
                var cachePath = Path.Combine(PathService.AlignmentCacheFolder, cacheFileName);

                // PNG 인코더로 저장
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(alignedImage));

                using (var stream = File.Create(cachePath))
                {
                    encoder.Save(stream);
                }

                Logger.Instance.Debug($"정렬된 이미지 캐시 저장: {cachePath}");
                return cachePath;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("정렬된 이미지 캐시 저장 실패", ex);
                throw;
            }
        }

        /// <summary>
        /// 선택된 문서의 오버레이 이미지를 생성하여 DisplayImage를 업데이트합니다.
        /// </summary>
        private void UpdateDisplayImage()
        {
            if (SelectedDocument == null)
            {
                DisplayImage = null;
                return;
            }

            try
            {
                Logger.Instance.Debug($"오버레이 이미지 생성 시작: {SelectedDocument.SourcePath}");

                // 정렬된 이미지 경로 사용 (정렬 실패 시 원본 사용)
                var imagePath = SelectedDocument.GetImagePathForUse();
                
                if (!File.Exists(imagePath))
                {
                    Logger.Instance.Warning($"이미지 파일을 찾을 수 없음: {imagePath}");
                    DisplayImage = null;
                    return;
                }

                // 이미지 로드
                var originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.UriSource = new Uri(imagePath, UriKind.Absolute);
                originalImage.EndInit();
                originalImage.Freeze();

                var template = _workspace.Template;
                
                // DrawingVisual로 렌더링
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // 원본 이미지 그리기
                    drawingContext.DrawImage(originalImage, new Rect(0, 0, SelectedDocument.ImageWidth, SelectedDocument.ImageHeight));

                    // 템플릿의 타이밍 마크 그리기 (녹색)
                    var timingMarkPen = new Pen(Brushes.Green, 2.0);
                    foreach (var overlay in template.TimingMarks)
                    {
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        drawingContext.DrawRectangle(null, timingMarkPen, rect);
                    }
                    
                    // 템플릿의 채점 영역 그리기
                    // 마킹 감지 결과가 있으면 결과에 따라 색상 변경
                    var scoringAreas = template.ScoringAreas.ToList();
                    for (int i = 0; i < scoringAreas.Count; i++)
                    {
                        var overlay = scoringAreas[i];
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        
                        // 마킹 감지 결과 확인
                        Brush? fillBrush = null;
                        Pen? pen = null;
                        
                        if (CurrentMarkingResults != null && i < CurrentMarkingResults.Count)
                        {
                            var result = CurrentMarkingResults[i];
                            if (result.IsMarked)
                            {
                                // 마킹 감지: 파란색 반투명 채우기 + 빨간색 테두리
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));
                                pen = new Pen(Brushes.Blue, 2.0);
                            }
                            else
                            {
                                // 미마킹: 빨간색 반투명 채우기 + 빨간색 테두리
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
                                pen = new Pen(Brushes.Red, 2.0);
                            }
                        }
                        else
                        {
                            // 마킹 감지 결과 없음: 빨간색 테두리만
                            pen = new Pen(Brushes.Red, 2.0);
                        }
                        
                        drawingContext.DrawRectangle(fillBrush, pen, rect);
                    }
                }

                // RenderTargetBitmap으로 변환
                var rtb = new RenderTargetBitmap(SelectedDocument.ImageWidth, SelectedDocument.ImageHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);
                rtb.Freeze();

                DisplayImage = rtb;
                Logger.Instance.Debug($"오버레이 이미지 생성 완료: {SelectedDocument.SourcePath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"오버레이 이미지 생성 실패: {SelectedDocument.SourcePath}", ex);
                DisplayImage = null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

