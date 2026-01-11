using System;
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
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly StateStore _stateStore;
        private readonly ImageLoader _imageLoader;
        private readonly Renderer _renderer;
        private readonly CoordinateConverter _coordConverter;
        private readonly ImageAlignmentService _alignmentService;
        private TemplateViewModel? _templateViewModel;
        // MarkingViewModel은 NavigationViewModel을 통해 관리되므로 더 이상 필요하지 않음
        // private MarkingViewModel? _markingViewModel;

        private Workspace _workspace;
        private ImageDocument? _selectedDocument;
        private RectangleOverlay? _selectedOverlay;
        private double _defaultRectWidth = 30;
        private double _defaultRectHeight = 30;
        private bool _isAddMode = false;
        private OverlayType _currentOverlayType = OverlayType.ScoringArea;
        private Rect _currentImageDisplayRect;

        public MainViewModel()
        {
            Logger.Instance.Info("MainViewModel 초기화 시작");
            
            try
            {
                _stateStore = new StateStore();
                _imageLoader = new ImageLoader();
                _renderer = new Renderer();
                _coordConverter = new CoordinateConverter();
                _alignmentService = new ImageAlignmentService();

                _workspace = new Workspace();
                _currentImageDisplayRect = new Rect();

                LoadWorkspace();

                AddRectangleCommand = new RelayCommand(() => IsAddMode = !IsAddMode);
                DeleteSelectedCommand = new RelayCommand(OnDeleteSelected, () => SelectedOverlay != null);
                ClearAllCommand = new RelayCommand(OnClearAll, () => GetCurrentOverlayCollection()?.Count > 0);
                SaveCommand = new RelayCommand(OnSave);
                LoadFolderCommand = new RelayCommand(OnLoadFolder);
                
                Logger.Instance.Info("MainViewModel 초기화 완료");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("MainViewModel 초기화 실패", ex);
                throw;
            }
        }

        public Workspace Workspace
        {
            get => _workspace;
            set
            {
                Logger.Instance.Debug($"Workspace 변경 시작. 이전: {(_workspace != null ? "있음" : "null")}, 새: {(value != null ? "있음" : "null")}");
                
                _workspace = value ?? throw new ArgumentNullException(nameof(value));
                OnPropertyChanged();
                
                // Workspace 변경 시 SelectedDocument 동기화
                SelectedDocument = value?.SelectedDocument;
                
                // 템플릿 변경 감지
                if (value?.Template != null)
                {
                    // TemplateViewModel 업데이트
                    if (_templateViewModel != null)
                    {
                        _templateViewModel.Template = value.Template;
                    }
                    
                    value.Template.TimingMarks.CollectionChanged += (s, e) => 
                    {
                        OnPropertyChanged(nameof(DisplayOverlays));
                        OnPropertyChanged(nameof(CurrentOverlayCollection));
                        UpdateMarkingViewModel();
                    };
                    value.Template.ScoringAreas.CollectionChanged += (s, e) => 
                    {
                        OnPropertyChanged(nameof(DisplayOverlays));
                        OnPropertyChanged(nameof(CurrentOverlayCollection));
                        UpdateMarkingViewModel();
                    };
                }
                
                // Documents 컬렉션 변경 감지
                if (value != null)
                {
                    Logger.Instance.Debug($"Workspace.Documents 컬렉션 변경 감지 등록. 현재 문서 수: {value.Documents.Count}");
                    
                    value.Documents.CollectionChanged += (s, e) =>
                    {
                        Logger.Instance.Debug($"Documents.CollectionChanged 이벤트 발생. Action: {e.Action}, NewItems: {e.NewItems?.Count ?? 0}, OldItems: {e.OldItems?.Count ?? 0}");
                        Logger.Instance.Debug($"현재 SelectedDocument: {(SelectedDocument != null ? SelectedDocument.SourcePath : "null")}");
                        if (SelectedDocument != null)
                        {
                            Logger.Instance.Debug($"Documents.Contains(SelectedDocument): {value.Documents.Contains(SelectedDocument)}");
                        }
                        
                        // Documents가 변경되면 SelectedDocument 유효성 검사
                        if (SelectedDocument != null && !value.Documents.Contains(SelectedDocument))
                        {
                            Logger.Instance.Warning($"SelectedDocument가 Documents 컬렉션에 없어서 null로 설정");
                            SelectedDocument = null;
                        }
                    };
                }
                
                Logger.Instance.Debug($"Workspace 변경 완료");
            }
        }

        public ImageDocument? SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                try
                {
                    var oldValue = _selectedDocument?.SourcePath ?? "null";
                    var newValue = value?.SourcePath ?? "null";
                    
                    if (!ReferenceEquals(_selectedDocument, value))
                    {
                        Logger.Instance.Debug($"SelectedDocument 변경 시작. 이전: {oldValue}, 새: {newValue}");
                        
                        _selectedDocument = value;

                        if (Workspace != null)
                        {
                            var documentId = value?.ImageId ?? "null";
                            Logger.Instance.Debug($"Workspace.SelectedDocumentId 설정: {documentId}");
                            Workspace.SelectedDocumentId = value?.ImageId;
                        }

                        Logger.Instance.Debug($"OnPropertyChanged 호출 전");
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(DisplayOverlays));
                        // 문서 변경 시 마킹 ViewModel 업데이트는 NavigationViewModel을 통해 처리됨
                        Logger.Instance.Debug($"SelectedDocument 변경 완료");
                    }
                    else
                    {
                        Logger.Instance.Debug($"SelectedDocument 변경 스킵 (동일 참조): {newValue}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"SelectedDocument setter에서 예외 발생", ex);
                    throw;
                }
            }
        }

        public RectangleOverlay? SelectedOverlay
        {
            get => _selectedOverlay;
            set
            {
                _selectedOverlay = value;
                OnPropertyChanged();
            }
        }

        public double DefaultRectWidth
        {
            get => _defaultRectWidth;
            set
            {
                _defaultRectWidth = value;
                OnPropertyChanged();
            }
        }

        public double DefaultRectHeight
        {
            get => _defaultRectHeight;
            set
            {
                _defaultRectHeight = value;
                OnPropertyChanged();
            }
        }

        public bool IsAddMode
        {
            get => _isAddMode;
            set
            {
                _isAddMode = value;
                OnPropertyChanged();
            }
        }

        public OverlayType CurrentOverlayType
        {
            get => _currentOverlayType;
            set
            {
                _currentOverlayType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentOverlayCollection));
            }
        }

        public Rect CurrentImageDisplayRect
        {
            get => _currentImageDisplayRect;
            set
            {
                _currentImageDisplayRect = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddRectangleCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand LoadFolderCommand { get; }

        // 분리된 ViewModel 속성
        public TemplateViewModel TemplateViewModel => _templateViewModel ?? throw new InvalidOperationException("TemplateViewModel이 초기화되지 않았습니다.");
        // MarkingViewModel은 NavigationViewModel을 통해 관리되므로 제거됨
        // public MarkingViewModel MarkingViewModel => _markingViewModel ?? throw new InvalidOperationException("MarkingViewModel이 초기화되지 않았습니다.");

        private void LoadWorkspace()
        {
            Logger.Instance.Info("Workspace 로드 시작");
            try
            {
                Workspace = _stateStore.Load();
                Logger.Instance.Info($"Workspace 로드 완료. Documents 수: {Workspace.Documents.Count}, SelectedDocumentId: {Workspace.SelectedDocumentId ?? "null"}");
                
                if (string.IsNullOrEmpty(Workspace.InputFolderPath))
                {
                    Workspace.InputFolderPath = PathService.DefaultInputFolder;
                    Logger.Instance.Info($"기본 InputFolderPath 설정: {PathService.DefaultInputFolder}");
                }
                
                // 템플릿이 비어있으면 기본 템플릿 로드 시도
                if (Workspace.Template.TimingMarks.Count == 0 && 
                    Workspace.Template.ScoringAreas.Count == 0 && 
                    Workspace.Template.ReferenceWidth == 0 && 
                    Workspace.Template.ReferenceHeight == 0)
                {
                    Logger.Instance.Info("템플릿이 비어있어 기본 템플릿 로드 시도");
                    var defaultTemplate = _stateStore.LoadDefaultTemplate();
                    if (defaultTemplate != null)
                    {
                        Logger.Instance.Info("기본 템플릿 로드 성공");
                        Workspace.Template.ReferenceWidth = defaultTemplate.ReferenceWidth;
                        Workspace.Template.ReferenceHeight = defaultTemplate.ReferenceHeight;
                        
                        foreach (var overlay in defaultTemplate.TimingMarks)
                        {
                            Workspace.Template.TimingMarks.Add(new RectangleOverlay
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
                            Workspace.Template.ScoringAreas.Add(new RectangleOverlay
                            {
                                X = overlay.X,
                                Y = overlay.Y,
                                Width = overlay.Width,
                                Height = overlay.Height,
                                StrokeThickness = overlay.StrokeThickness,
                                OverlayType = overlay.OverlayType
                            });
                        }
                        
                        OnPropertyChanged(nameof(DisplayOverlays));
                        OnPropertyChanged(nameof(CurrentOverlayCollection));
                    }
                    else
                    {
                        Logger.Instance.Info("기본 템플릿이 없음");
                    }
                }
                
                // Workspace 로드 후 SelectedDocument 초기화
                SelectedDocument = Workspace?.SelectedDocument;
                Logger.Instance.Info($"SelectedDocument 초기화 완료: {(SelectedDocument != null ? SelectedDocument.SourcePath : "null")}");

                // 분리된 ViewModel 초기화 (Workspace 로드 후)
                if (Workspace != null && Workspace.Template != null)
                {
                    if (_templateViewModel == null)
                    {
                        _templateViewModel = new TemplateViewModel(_stateStore, Workspace.Template);
                    }
                    else
                    {
                        _templateViewModel.Template = Workspace.Template;
                    }
                }

                // MainViewModel은 레거시 코드로 사용되지 않음
                // MarkingViewModel은 NavigationViewModel 기반 구조에서 생성됨
                // if (_markingViewModel == null)
                // {
                //     _markingViewModel = new MarkingViewModel(new MarkingDetector(), null);
                // }
                // if (Workspace != null && Workspace.Template != null)
                // {
                //     UpdateMarkingViewModel();
                // }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Workspace 로드 실패", ex);
                MessageBox.Show($"상태 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Workspace = new Workspace { InputFolderPath = PathService.DefaultInputFolder };
                SelectedDocument = null;
            }
        }

        public void OnCanvasClick(Point screenPoint, Size canvasSize)
        {
            if (!IsAddMode || SelectedDocument == null)
            {
                Logger.Instance.Debug($"OnCanvasClick 스킵. IsAddMode: {IsAddMode}, SelectedDocument: {(SelectedDocument != null ? "있음" : "null")}");
                return;
            }

            Logger.Instance.Debug($"캔버스 클릭. 화면 좌표: ({screenPoint.X}, {screenPoint.Y}), 캔버스 크기: {canvasSize.Width}x{canvasSize.Height}");
            
            try
            {
                // 화면 좌표 → 원본 픽셀 좌표 변환
                var pixelPoint = _coordConverter.ScreenToPixel(
                    screenPoint,
                    canvasSize,
                    SelectedDocument.ImageWidth,
                    SelectedDocument.ImageHeight,
                    CurrentImageDisplayRect);
                
                Logger.Instance.Debug($"픽셀 좌표 변환 완료: ({pixelPoint.X}, {pixelPoint.Y})");

                // 기본 크기로 사각형 생성 (클릭 위치를 중심으로)
                var overlay = new RectangleOverlay
                {
                    X = pixelPoint.X - DefaultRectWidth / 2,
                    Y = pixelPoint.Y - DefaultRectHeight / 2,
                    Width = DefaultRectWidth,
                    Height = DefaultRectHeight
                };

                // 경계 체크
                overlay.X = Math.Max(0, Math.Min(overlay.X, SelectedDocument.ImageWidth - overlay.Width));
                overlay.Y = Math.Max(0, Math.Min(overlay.Y, SelectedDocument.ImageHeight - overlay.Height));

                overlay.OverlayType = CurrentOverlayType;
                
                Logger.Instance.Info($"오버레이 추가. 타입: {CurrentOverlayType}, 위치: ({overlay.X}, {overlay.Y}), 크기: {overlay.Width}x{overlay.Height}");
                
                // 템플릿의 적절한 컬렉션에 추가
                var collection = GetCurrentOverlayCollection();
                if (collection != null)
                {
                    collection.Add(overlay);
                    SelectedOverlay = overlay;
                    
                    // 템플릿 기준 크기 업데이트 (첫 번째 오버레이 추가 시)
                    if (Workspace.Template.ReferenceWidth == 0 && SelectedDocument != null)
                    {
                        Workspace.Template.ReferenceWidth = SelectedDocument.ImageWidth;
                        Workspace.Template.ReferenceHeight = SelectedDocument.ImageHeight;
                    }
                    
                    OnPropertyChanged(nameof(DisplayOverlays));
                    OnPropertyChanged(nameof(CurrentOverlayCollection));
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("사각형 추가 실패", ex);
                MessageBox.Show($"사각형 추가 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateImageDisplayRect(Size availableSize)
        {
            if (SelectedDocument != null)
            {
                var newRect = ZoomHelper.CalculateImageDisplayRect(
                    SelectedDocument.ImageWidth,
                    SelectedDocument.ImageHeight,
                    availableSize,
                    ZoomHelper.ImageAlignment.TopLeft); // 왼쪽 위 정렬
                
                // 값이 실제로 변경되었을 때만 PropertyChanged 발생 (무한 루프 방지)
                // 부동소수점 비교를 위해 작은 오차 허용
                const double epsilon = 0.001;
                if (Math.Abs(CurrentImageDisplayRect.X - newRect.X) > epsilon ||
                    Math.Abs(CurrentImageDisplayRect.Y - newRect.Y) > epsilon ||
                    Math.Abs(CurrentImageDisplayRect.Width - newRect.Width) > epsilon ||
                    Math.Abs(CurrentImageDisplayRect.Height - newRect.Height) > epsilon)
                {
                    CurrentImageDisplayRect = newRect;
                }
            }
        }

        private void OnDeleteSelected()
        {
            if (SelectedOverlay != null)
            {
                // 템플릿의 적절한 컬렉션에서 제거
                if (Workspace.Template.TimingMarks.Contains(SelectedOverlay))
                {
                    Workspace.Template.TimingMarks.Remove(SelectedOverlay);
                }
                else if (Workspace.Template.ScoringAreas.Contains(SelectedOverlay))
                {
                    Workspace.Template.ScoringAreas.Remove(SelectedOverlay);
                }
                
                SelectedOverlay = null;
                OnPropertyChanged(nameof(DisplayOverlays));
                OnPropertyChanged(nameof(CurrentOverlayCollection));
            }
        }

        private void OnClearAll()
        {
            var collection = GetCurrentOverlayCollection();
            if (collection != null && collection.Count > 0)
            {
                var result = MessageBox.Show(
                    $"모든 {CurrentOverlayType} 오버레이를 삭제하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    collection.Clear();
                    SelectedOverlay = null;
                    OnPropertyChanged(nameof(DisplayOverlays));
                    OnPropertyChanged(nameof(CurrentOverlayCollection));
                }
            }
        }

        /// <summary>
        /// 현재 선택된 오버레이 타입에 해당하는 컬렉션을 반환합니다.
        /// </summary>
        private System.Collections.ObjectModel.ObservableCollection<RectangleOverlay>? GetCurrentOverlayCollection()
        {
            return CurrentOverlayType switch
            {
                OverlayType.TimingMark => Workspace.Template.TimingMarks,
                OverlayType.ScoringArea => Workspace.Template.ScoringAreas,
                _ => null
            };
        }

        /// <summary>
        /// 현재 선택된 이미지에 표시할 모든 오버레이를 반환합니다 (템플릿 기반).
        /// </summary>
        public System.Collections.Generic.IEnumerable<RectangleOverlay> DisplayOverlays
        {
            get
            {
                if (SelectedDocument == null)
                {
                    return Enumerable.Empty<RectangleOverlay>();
                }

                // 템플릿의 모든 오버레이를 반환
                // (나중에 정렬 기능이 추가되면 여기서 변환 적용)
                return Workspace.Template.TimingMarks.Concat(Workspace.Template.ScoringAreas);
            }
        }

        /// <summary>
        /// 현재 선택된 오버레이 타입의 컬렉션을 반환합니다 (UI 바인딩용).
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<RectangleOverlay> CurrentOverlayCollection
        {
            get
            {
                return CurrentOverlayType switch
                {
                    OverlayType.TimingMark => Workspace.Template.TimingMarks,
                    OverlayType.ScoringArea => Workspace.Template.ScoringAreas,
                    _ => Workspace.Template.ScoringAreas
                };
            }
        }

        private void OnSave()
        {
            Logger.Instance.Info("저장 시작");
            try
            {
                Logger.Instance.Debug($"저장할 Workspace. Documents 수: {Workspace.Documents.Count}");
                _stateStore.Save(Workspace);
                Logger.Instance.Info("상태 저장 완료");
                
                Logger.Instance.Debug("렌더링 시작");
                _renderer.RenderAll(Workspace);
                Logger.Instance.Info("렌더링 완료");
                
                Logger.Instance.Info($"저장 완료. 출력 폴더: {PathService.OutputFolder}");
                MessageBox.Show(
                    $"저장 완료!\n출력 폴더: {PathService.OutputFolder}",
                    "저장 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("저장 실패", ex);
                MessageBox.Show($"저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnLoadFolder()
        {
            Logger.Instance.Info("폴더 로드 시작");
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "이미지가 있는 폴더를 선택하세요",
                    SelectedPath = Workspace.InputFolderPath
                };

                Logger.Instance.Debug($"FolderBrowserDialog 표시. 초기 경로: {Workspace.InputFolderPath}");
                
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
                    
                    Workspace.InputFolderPath = folderPath;
                    Logger.Instance.Debug($"Workspace.InputFolderPath 설정: {folderPath}");
                    
                    Logger.Instance.Debug("Workspace.Documents.Clear() 호출");
                    Workspace.Documents.Clear();
                    
                    Logger.Instance.Debug($"문서 {documents.Count}개 추가 및 정렬 적용 시작");
                    foreach (var doc in documents)
                    {
                        // 이미지 정렬 적용
                        ApplyAlignmentToDocument(doc);
                        
                        Workspace.Documents.Add(doc);
                        Logger.Instance.Debug($"문서 추가: {doc.SourcePath} (ID: {doc.ImageId}, 크기: {doc.ImageWidth}x{doc.ImageHeight})");
                    }
                    Logger.Instance.Debug("모든 문서 추가 완료");

                    // 첫 번째 문서 선택 및 ViewModel 동기화
                    if (documents.Count > 0)
                    {
                        var firstDoc = documents[0];
                        Logger.Instance.Debug($"첫 번째 문서 선택. ImageId: {firstDoc.ImageId}, SourcePath: {firstDoc.SourcePath}");
                        
                        Workspace.SelectedDocumentId = firstDoc.ImageId;
                        Logger.Instance.Debug($"Workspace.SelectedDocumentId 설정: {firstDoc.ImageId}");
                        
                        SelectedDocument = firstDoc;  // ViewModel에도 직접 설정
                        Logger.Instance.Debug($"SelectedDocument 설정 완료: {firstDoc.SourcePath}");
                    }
                    else
                    {
                        Logger.Instance.Debug("문서가 없어서 SelectedDocument를 null로 설정");
                        SelectedDocument = null;
                    }

                    // MarkingViewModel 업데이트
                    UpdateMarkingViewModel();

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
                if (Workspace.Template.TimingMarks.Count == 0)
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
                var result = _alignmentService.AlignImage(bitmap, Workspace.Template);

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
        /// MarkingViewModel의 데이터 소스를 업데이트합니다.
        /// MarkingViewModel은 NavigationViewModel을 통해 관리되므로 더 이상 사용되지 않습니다.
        /// </summary>
        private void UpdateMarkingViewModel()
        {
            // MarkingViewModel은 NavigationViewModel을 통해 관리되므로 더 이상 필요하지 않음
            // 이 메서드는 호출되지만 실제로는 아무 작업도 수행하지 않음
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

