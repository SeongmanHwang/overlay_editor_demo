using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
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

        private Workspace _workspace;
        private ImageDocument? _selectedDocument;
        private RectangleOverlay? _selectedOverlay;
        private double _defaultRectWidth = 30;
        private double _defaultRectHeight = 30;
        private bool _isAddMode = false;
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

                _workspace = new Workspace();
                _currentImageDisplayRect = new Rect();

                LoadWorkspace();

                AddRectangleCommand = new RelayCommand(() => IsAddMode = !IsAddMode);
                DeleteSelectedCommand = new RelayCommand(OnDeleteSelected, () => SelectedOverlay != null);
                ClearAllCommand = new RelayCommand(OnClearAll, () => SelectedDocument?.Overlays.Count > 0);
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
                
                // Workspace 로드 후 SelectedDocument 초기화
                SelectedDocument = Workspace?.SelectedDocument;
                Logger.Instance.Info($"SelectedDocument 초기화 완료: {(SelectedDocument != null ? SelectedDocument.SourcePath : "null")}");
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

                Logger.Instance.Info($"오버레이 추가. 위치: ({overlay.X}, {overlay.Y}), 크기: {overlay.Width}x{overlay.Height}");
                SelectedDocument.Overlays.Add(overlay);
                SelectedOverlay = overlay;
                SelectedDocument.LastEditedAt = DateTime.Now;
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
            if (SelectedOverlay != null && SelectedDocument != null)
            {
                SelectedDocument.Overlays.Remove(SelectedOverlay);
                SelectedOverlay = null;
                SelectedDocument.LastEditedAt = DateTime.Now;
            }
        }

        private void OnClearAll()
        {
            if (SelectedDocument != null)
            {
                var result = MessageBox.Show(
                    "모든 오버레이를 삭제하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SelectedDocument.Overlays.Clear();
                    SelectedOverlay = null;
                    SelectedDocument.LastEditedAt = DateTime.Now;
                }
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
                    
                    Logger.Instance.Debug($"문서 {documents.Count}개 추가 시작");
                    foreach (var doc in documents)
                    {
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

