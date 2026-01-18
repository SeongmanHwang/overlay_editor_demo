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
        // ✅ _selectedOverlay 필드 제거: SelectedOverlay는 계산 프로퍼티로 변경
        private OverlaySelectionViewModel _selectionVM;
        private OverlayType _currentOverlayType = OverlayType.ScoringArea;
        private int? _currentQuestionNumber = 1; // ScoringArea일 때 사용 (1-4)
        private Rect _currentImageDisplayRect;
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
            
            // ✅ SelectionVM의 Selected 변경 감지 (Single Source of Truth)
            _selectionVM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(OverlaySelectionViewModel.Selected))
                {
                    // SelectedOverlay는 계산 프로퍼티이므로 자동으로 갱신됨
                    OnPropertyChanged(nameof(SelectedOverlay));
                }
                else if (e.PropertyName is nameof(OverlaySelectionViewModel.X)
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
                // ✅ 오버레이 컬렉션 변경 시 선택 자동 정리
                void OnOverlayCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
                {
                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove ||
                        e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Replace)
                    {
                        // 삭제된 오버레이를 선택에서 제거
                        var removedOverlays = e.OldItems?.Cast<RectangleOverlay>() ?? Enumerable.Empty<RectangleOverlay>();
                        foreach (var overlay in removedOverlays)
                        {
                            _selectionVM.Remove(overlay);
                        }
                    }
                    
                    OnPropertyChanged(nameof(DisplayOverlays));
                    OnPropertyChanged(nameof(CurrentOverlayCollection));
                }
                
                _workspace.Template.TimingMarks.CollectionChanged += OnOverlayCollectionChanged;
                _workspace.Template.ScoringAreas.CollectionChanged += OnOverlayCollectionChanged;
                _workspace.Template.BarcodeAreas.CollectionChanged += OnOverlayCollectionChanged;
                _workspace.Template.Questions.CollectionChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(DisplayOverlays));
                    OnPropertyChanged(nameof(CurrentOverlayCollection));
                };
                foreach (var question in _workspace.Template.Questions)
                {
                    question.Options.CollectionChanged += OnOverlayCollectionChanged;
                }
            }

            // Commands
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

        /// <summary>
        /// 선택된 오버레이 (단일 선택, 하위 호환성용)
        /// ✅ 계산 프로퍼티: SelectionVM이 Single Source of Truth
        /// </summary>
        public RectangleOverlay? SelectedOverlay
        {
            get => _selectionVM.Selected.FirstOrDefault();
            set
            {
                // setter는 하위 호환성을 위해 유지하되, SelectionVM만 수정
                if (value != null)
                {
                    _selectionVM.SetSelection(new[] { value });
                }
                else
                {
                    _selectionVM.Clear();
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
        /// 선택 가능한 문항 번호 목록 (1부터 {OmrConstants.QuestionsCount}까지)
        /// </summary>
        public System.Collections.Generic.IEnumerable<int> QuestionNumbers
        {
            get
            {
                for (int i = 1; i <= OmrConstants.QuestionsCount; i++)
                {
                    yield return i;
                }
            }
        }

        /// <summary>
        /// 다중 선택 관리 ViewModel
        /// </summary>
        public OverlaySelectionViewModel SelectionVM => _selectionVM;

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
        /// 다음 빈 슬롯 반환 (ScoringArea일 때)
        /// </summary>
        private RectangleOverlay? GetNextEmptySlot()
        {
            if (CurrentOverlayType != OverlayType.ScoringArea || !CurrentQuestionNumber.HasValue)
                return null;

            var question = _workspace.Template.Questions
                .FirstOrDefault(q => q.QuestionNumber == CurrentQuestionNumber.Value);

            return question?.Options.FirstOrDefault(o => !o.IsPlaced);
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

        // 고정 슬롯 정책: TemplateEdit에서 추가/삭제(=unplace 포함) 기능은 제거됨.

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
            
            // ✅ SelectedOverlay는 계산 프로퍼티이므로 자동으로 갱신됨
            // OnPropertyChanged는 SelectionVM.PropertyChanged에서 자동 호출됨
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










