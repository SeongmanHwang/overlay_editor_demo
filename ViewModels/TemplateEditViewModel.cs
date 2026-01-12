using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Utils;
using SimpleOverlayEditor.Utils.Commands;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 템플릿 편집 모드용 ViewModel입니다.
    /// </summary>
    public class TemplateEditViewModel : INotifyPropertyChanged
    {
        private readonly StateStore _stateStore;
        private readonly TemplateStore _templateStore;
        private readonly ImageLoader _imageLoader;
        private readonly CoordinateConverter _coordConverter;
        private readonly NavigationViewModel _navigation;
        private readonly UndoManager _undoManager;
        private TemplateViewModel? _templateViewModel;

        private Workspace _workspace;
        private ImageDocument? _selectedDocument;
        private RectangleOverlay? _selectedOverlay;
        private OverlaySelectionViewModel _selectionVM;
        private OverlayType _currentOverlayType = OverlayType.ScoringArea;
        private int? _currentQuestionNumber = 1; // ScoringArea일 때 사용 (1-4)
        private Rect _currentImageDisplayRect;
        private bool _isAddMode = false;
        private double _zoomLevel = 1.0; // 줌 레벨 (1.0 = 100%)

        public TemplateEditViewModel(NavigationViewModel navigation, Workspace workspace, StateStore stateStore)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _templateStore = new TemplateStore();
            _imageLoader = new ImageLoader();
            _coordConverter = new CoordinateConverter();
            _undoManager = new UndoManager();

            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _currentImageDisplayRect = new Rect();

            // SelectionVM 초기화
            _selectionVM = new OverlaySelectionViewModel();
            _selectionVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(OverlaySelectionViewModel.X)
                    or nameof(OverlaySelectionViewModel.Y)
                    or nameof(OverlaySelectionViewModel.Width)
                    or nameof(OverlaySelectionViewModel.Height))
                {
                    OnPropertyChanged(nameof(SelectionVM));
                }
            };

            // TemplateViewModel 초기화
            _templateViewModel = new TemplateViewModel(_stateStore, _workspace.Template);

            // 템플릿 변경 감지
            if (_workspace.Template != null)
            {
                _workspace.Template.TimingMarks.CollectionChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(DisplayOverlays));
                    OnPropertyChanged(nameof(CurrentOverlayCollection));
                };
                _workspace.Template.ScoringAreas.CollectionChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(DisplayOverlays));
                    OnPropertyChanged(nameof(CurrentOverlayCollection));
                };
                _workspace.Template.BarcodeAreas.CollectionChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(DisplayOverlays));
                    OnPropertyChanged(nameof(CurrentOverlayCollection));
                };
                _workspace.Template.Questions.CollectionChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(DisplayOverlays));
                    OnPropertyChanged(nameof(CurrentOverlayCollection));
                };
                foreach (var question in _workspace.Template.Questions)
                {
                    question.Options.CollectionChanged += (s, e) =>
                    {
                        OnPropertyChanged(nameof(DisplayOverlays));
                        OnPropertyChanged(nameof(CurrentOverlayCollection));
                    };
                }
            }

            // Commands
            ToggleAddModeCommand = new RelayCommand(() => IsAddMode = !IsAddMode);
            DeleteSelectedCommand = new RelayCommand(OnDeleteSelected, () => !_selectionVM.IsEmpty);
            ClearAllCommand = new RelayCommand(OnClearAll, () => GetCurrentOverlayCollection()?.Count > 0);
            SaveTemplateCommand = new RelayCommand(OnSaveTemplate);
            LoadSampleImageCommand = new RelayCommand(OnLoadSampleImage);
            NavigateToHomeCommand = new RelayCommand(() => _navigation.NavigateTo(ApplicationMode.Home));

            // 정렬 명령
            AlignLeftCommand = new RelayCommand(OnAlignLeft, () => _selectionVM.Selected.Count >= 2);
            AlignTopCommand = new RelayCommand(OnAlignTop, () => _selectionVM.Selected.Count >= 2);

            // 실행 취소 명령
            UndoCommand = new RelayCommand(OnUndo, () => _undoManager.CanUndo);

            Logger.Instance.Info("TemplateEditViewModel 초기화 완료");
        }

        public Workspace Workspace
        {
            get => _workspace;
            set
            {
                _workspace = value ?? throw new ArgumentNullException(nameof(value));
                OnPropertyChanged();
                // SelectedDocument는 Session에서 관리되므로 Workspace에서는 처리하지 않음
                SelectedDocument = null;
            }
        }

        /// <summary>
        /// 샘플 이미지 (원본, 정렬하지 않음)
        /// </summary>
        public ImageDocument? SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                if (!ReferenceEquals(_selectedDocument, value))
                {
                    _selectedDocument = value;

                    if (_workspace != null)
                    {
                        _workspace.SelectedDocumentId = value?.ImageId;
                    }

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayOverlays));
                    UpdateImageDisplayRect(new Size(800, 600)); // 기본 크기
                }
            }
        }

        public RectangleOverlay? SelectedOverlay
        {
            get => _selectedOverlay;
            set
            {
                if (_selectedOverlay != value)
                {
                    _selectedOverlay = value;
                    OnPropertyChanged();
                    
                    // SelectionVM 동기화
                    if (value != null)
                    {
                        _selectionVM.SetSelection(new[] { value });
                    }
                    else
                    {
                        _selectionVM.Clear();
                    }
                    
                    // 오버레이 선택 시 추가 모드 해제
                    if (_selectedOverlay != null)
                    {
                        IsAddMode = false;
                    }
                }
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
                OnPropertyChanged(nameof(IsQuestionNumberVisible));
            }
        }

        /// <summary>
        /// 현재 선택된 문항 번호 (ScoringArea일 때만 사용, 1-4)
        /// </summary>
        public int? CurrentQuestionNumber
        {
            get => _currentQuestionNumber;
            set
            {
                _currentQuestionNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentOverlayCollection));
            }
        }

        /// <summary>
        /// 문항 번호 선택이 표시되어야 하는지 여부 (ScoringArea일 때만)
        /// </summary>
        public bool IsQuestionNumberVisible => CurrentOverlayType == OverlayType.ScoringArea;

        /// <summary>
        /// 다중 선택 관리 ViewModel
        /// </summary>
        public OverlaySelectionViewModel SelectionVM => _selectionVM;

        /// <summary>
        /// 오버레이 추가 모드 활성화 여부
        /// </summary>
        public bool IsAddMode
        {
            get => _isAddMode;
            set
            {
                if (_isAddMode != value)
                {
                    _isAddMode = value;
                    OnPropertyChanged();
                    
                    // 추가 모드 활성화 시 선택 해제
                    if (_isAddMode)
                    {
                        SelectedOverlay = null;
                    }
                }
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

        /// <summary>
        /// 이미지 줌 레벨 (1.0 = 100%, 최소 0.1, 최대 5.0)
        /// </summary>
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                var clampedValue = Math.Max(0.1, Math.Min(5.0, value));
                if (Math.Abs(_zoomLevel - clampedValue) > 0.001)
                {
                    _zoomLevel = clampedValue;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayOverlays)); // 오버레이도 줌에 맞게 다시 그리기
                }
            }
        }

        public ICommand ToggleAddModeCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand SaveTemplateCommand { get; }
        public ICommand LoadSampleImageCommand { get; }
        public ICommand NavigateToHomeCommand { get; }

        // 정렬 명령
        public ICommand AlignLeftCommand { get; }
        public ICommand AlignTopCommand { get; }

        // 실행 취소 명령
        public ICommand UndoCommand { get; }

        public TemplateViewModel TemplateViewModel => _templateViewModel ?? throw new InvalidOperationException("TemplateViewModel이 초기화되지 않았습니다.");

        /// <summary>
        /// 현재 선택된 오버레이 타입의 컬렉션을 반환합니다.
        /// ScoringArea일 때는 현재 선택된 문항의 Options를 반환합니다.
        /// </summary>
        private System.Collections.ObjectModel.ObservableCollection<RectangleOverlay>? GetCurrentOverlayCollection()
        {
            return CurrentOverlayType switch
            {
                OverlayType.TimingMark => _workspace.Template.TimingMarks,
                OverlayType.ScoringArea => CurrentQuestionNumber.HasValue
                    ? _workspace.Template.Questions.FirstOrDefault(q => q.QuestionNumber == CurrentQuestionNumber.Value)?.Options
                    : null,
                OverlayType.BarcodeArea => _workspace.Template.BarcodeAreas,
                _ => null
            };
        }

        /// <summary>
        /// 현재 선택된 이미지에 표시할 모든 오버레이를 반환합니다.
        /// ScoringAreas는 Questions.Options에서 자동으로 동기화되므로 ScoringAreas를 사용합니다.
        /// </summary>
        public System.Collections.Generic.IEnumerable<RectangleOverlay> DisplayOverlays
        {
            get
            {
                if (SelectedDocument == null)
                {
                    return Enumerable.Empty<RectangleOverlay>();
                }

                // ScoringAreas는 Questions.Options에서 자동으로 동기화되므로
                // 표시용으로는 ScoringAreas를 사용 (편집은 Questions.Options에서 수행)
                return _workspace.Template.TimingMarks
                    .Concat(_workspace.Template.ScoringAreas)
                    .Concat(_workspace.Template.BarcodeAreas);
            }
        }

        /// <summary>
        /// 현재 선택된 오버레이 타입의 컬렉션을 반환합니다 (UI 바인딩용).
        /// ScoringArea일 때는 현재 선택된 문항의 Options를 반환합니다.
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<RectangleOverlay>? CurrentOverlayCollection
        {
            get
            {
                return CurrentOverlayType switch
                {
                    OverlayType.TimingMark => _workspace.Template.TimingMarks,
                    OverlayType.ScoringArea => CurrentQuestionNumber.HasValue
                        ? _workspace.Template.Questions.FirstOrDefault(q => q.QuestionNumber == CurrentQuestionNumber.Value)?.Options
                        : null,
                    OverlayType.BarcodeArea => _workspace.Template.BarcodeAreas,
                    _ => null
                };
            }
        }

        public void OnCanvasClick(Point screenPoint, Size canvasSize)
        {
            if (!IsAddMode)
            {
                Logger.Instance.Debug($"OnCanvasClick 스킵. IsAddMode: false");
                return;
            }

            if (SelectedDocument == null)
            {
                Logger.Instance.Debug($"OnCanvasClick 스킵. SelectedDocument: null");
                return;
            }

            // ScoringArea일 때는 문항 번호가 선택되어 있어야 함
            if (CurrentOverlayType == OverlayType.ScoringArea && !CurrentQuestionNumber.HasValue)
            {
                Logger.Instance.Debug($"OnCanvasClick 스킵. ScoringArea 선택되었지만 문항 번호가 없음");
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
                const double defaultWidth = 45.0;
                const double defaultHeight = 41.0;
                var overlay = new RectangleOverlay
                {
                    X = pixelPoint.X - defaultWidth / 2,
                    Y = pixelPoint.Y - defaultHeight / 2,
                    Width = defaultWidth,
                    Height = defaultHeight
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
                    var command = new AddOverlayCommand(overlay, collection);
                    _undoManager.ExecuteCommand(command);
                    SelectedOverlay = overlay;

                    // 템플릿 기준 크기 업데이트 (첫 번째 오버레이 추가 시)
                    if (_workspace.Template.ReferenceWidth == 0 && SelectedDocument != null)
                    {
                        _workspace.Template.ReferenceWidth = SelectedDocument.ImageWidth;
                        _workspace.Template.ReferenceHeight = SelectedDocument.ImageHeight;
                    }

                    OnPropertyChanged(nameof(DisplayOverlays));
                    OnPropertyChanged(nameof(CurrentOverlayCollection));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
                // 기본 표시 크기 계산 (줌 없이)
                var baseRect = ZoomHelper.CalculateImageDisplayRect(
                    SelectedDocument.ImageWidth,
                    SelectedDocument.ImageHeight,
                    availableSize,
                    ZoomHelper.ImageAlignment.TopLeft);

                // 줌 레벨을 적용하여 실제 표시 크기 계산
                var newRect = new Rect(
                    baseRect.X * ZoomLevel,
                    baseRect.Y * ZoomLevel,
                    baseRect.Width * ZoomLevel,
                    baseRect.Height * ZoomLevel);

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
            if (SelectedOverlay == null) return;

            var overlay = SelectedOverlay;
            IUndoableCommand? command = null;

            // TimingMarks와 BarcodeAreas는 직접 삭제
            if (_workspace.Template.TimingMarks.Contains(overlay))
            {
                command = new DeleteOverlayCommand(
                    overlay,
                    _workspace.Template.TimingMarks,
                    OverlayType.TimingMark);
            }
            else if (_workspace.Template.BarcodeAreas.Contains(overlay))
            {
                command = new DeleteOverlayCommand(
                    overlay,
                    _workspace.Template.BarcodeAreas,
                    OverlayType.BarcodeArea);
            }
            // ScoringAreas는 Questions.Options에서 삭제해야 함 (ScoringAreas는 자동 동기화됨)
            else if (_workspace.Template.ScoringAreas.Contains(overlay))
            {
                // Questions의 Options에서 찾아서 삭제
                Question? parentQuestion = null;
                foreach (var question in _workspace.Template.Questions)
                {
                    if (question.Options.Contains(overlay))
                    {
                        parentQuestion = question;
                        break;
                    }
                }

                if (parentQuestion != null)
                {
                    command = new DeleteOverlayCommand(
                        overlay,
                        parentQuestion.Options,
                        OverlayType.ScoringArea,
                        parentQuestion);
                }
            }

            if (command != null)
            {
                _undoManager.ExecuteCommand(command);
                SelectedOverlay = null;
                OnPropertyChanged(nameof(DisplayOverlays));
                OnPropertyChanged(nameof(CurrentOverlayCollection));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
        /// 템플릿 저장 (template.json에 저장)
        /// </summary>
        private void OnSaveTemplate()
        {
            Logger.Instance.Info("템플릿 저장 시작");
            try
            {
                _templateStore.Save(_workspace.Template);
                Logger.Instance.Info("템플릿 저장 완료");
                MessageBox.Show(
                    "템플릿이 저장되었습니다.",
                    "저장 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("템플릿 저장 실패", ex);
                MessageBox.Show($"템플릿 저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 샘플 이미지 로드 (원본만, 정렬하지 않음)
        /// </summary>
        private void OnLoadSampleImage()
        {
            Logger.Instance.Info("샘플 이미지 로드 시작");
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "이미지 파일|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff|모든 파일|*.*",
                    Title = "샘플 이미지 선택"
                };

                if (dialog.ShowDialog() == true)
                {
                    var imagePath = dialog.FileName;
                    Logger.Instance.Info($"선택된 샘플 이미지: {imagePath}");

                    // 이미지 정보 로드
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // 샘플 이미지는 원본 그대로 사용 (정렬하지 않음)
                    var document = new ImageDocument
                    {
                        ImageId = Guid.NewGuid().ToString(),
                        SourcePath = imagePath,
                        ImageWidth = bitmap.PixelWidth,
                        ImageHeight = bitmap.PixelHeight,
                        AlignmentInfo = null // 정렬 정보 없음
                    };

                    SelectedDocument = document;
                    
                    // Documents는 Session에서 관리되므로 Workspace에서는 처리하지 않음
                    // 템플릿 편집 모드에서는 SelectedDocument만 사용

                    Logger.Instance.Info($"샘플 이미지 로드 완료: {imagePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("샘플 이미지 로드 실패", ex);
                MessageBox.Show($"샘플 이미지 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 오버레이가 현재 선택 가능한지 확인 (현재 문항에 속하는지)
        /// </summary>
        public bool IsOverlaySelectable(RectangleOverlay overlay)
        {
            if (CurrentOverlayType != OverlayType.ScoringArea)
            {
                // ScoringArea가 아니면 항상 선택 가능
                return true;
            }

            // ScoringArea인 경우, 현재 선택된 문항에 속한 것만 선택 가능
            if (!CurrentQuestionNumber.HasValue) return false;

            var question = _workspace.Template.Questions
                .FirstOrDefault(q => q.QuestionNumber == CurrentQuestionNumber.Value);

            return question?.Options.Contains(overlay) ?? false;
        }

        /// <summary>
        /// DataGrid 선택 동기화
        /// </summary>
        public void SyncSelectionFromDataGrid(System.Collections.IList selectedItems)
        {
            var overlays = selectedItems.Cast<RectangleOverlay>()
                .Where(o => IsOverlaySelectable(o))
                .ToList();
            _selectionVM.SetSelection(overlays);

            // 단일 선택도 업데이트 (하위 호환)
            _selectedOverlay = overlays.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedOverlay));
        }

        // 정렬 메서드들
        private void OnAlignLeft()
        {
            if (_selectionVM.Selected.Count < 2) return;
            var command = new AlignLeftCommand(_selectionVM.Selected);
            _undoManager.ExecuteCommand(command);
            OnPropertyChanged(nameof(DisplayOverlays));
            OnPropertyChanged(nameof(CurrentOverlayCollection));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void OnAlignTop()
        {
            if (_selectionVM.Selected.Count < 2) return;
            var command = new AlignTopCommand(_selectionVM.Selected);
            _undoManager.ExecuteCommand(command);
            OnPropertyChanged(nameof(DisplayOverlays));
            OnPropertyChanged(nameof(CurrentOverlayCollection));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void OnUndo()
        {
            _undoManager.Undo();
            OnPropertyChanged(nameof(DisplayOverlays));
            OnPropertyChanged(nameof(CurrentOverlayCollection));
            // Command의 CanExecute 업데이트
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}










