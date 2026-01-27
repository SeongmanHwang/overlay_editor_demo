using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 마킹 리딩 전용 ViewModel입니다.
    /// </summary>
    public partial class MarkingViewModel : INotifyPropertyChanged, INavigationAware, IRoundAware
    {
        private readonly MarkingDetector _markingDetector;
        private readonly BarcodeReaderService _barcodeReaderService;
        private readonly NavigationViewModel _navigation;
        private readonly Workspace _workspace;
        private readonly StateStore _stateStore;
        private readonly SessionStore _sessionStore;
        private Session _session;
        private readonly ImageLoader _imageLoader;
        private readonly ImageAlignmentService _alignmentService;
        private readonly Renderer _renderer;
        private readonly MarkingAnalyzer _markingAnalyzer;
        private List<MarkingResult>? _currentMarkingResults;
        private List<BarcodeResult>? _currentBarcodeResults;
        private double _markingThreshold = 220.0;
        private BitmapSource? _displayImage;
        private ObservableCollection<OmrSheetResult>? _sheetResults;
        private ICollectionView? _filteredSheetResults;
        private string _filterMode = "All";
        private Rect _currentImageDisplayRect;
        private double _zoomLevel = 1.0; // 줌 레벨 (1.0 = 100%)
        
        // 필터 옵션 (시각/실/순)
        private ObservableCollection<string> _sessionFilterOptions = new ObservableCollection<string>();
        private ObservableCollection<string> _roomFilterOptions = new ObservableCollection<string>();
        private ObservableCollection<string> _orderFilterOptions = new ObservableCollection<string>();
        private string? _selectedSessionFilter;
        private string? _selectedRoomFilter;
        private string? _selectedOrderFilter;
        private int _readyForReadingCount;
        private ImageDocument? _selectedDocument;
        private readonly object _ingestStateLock = new object();

        /// <summary>
        /// SheetResults 항목의 PropertyChanged 이벤트를 처리합니다.
        /// </summary>
        private void OnSheetResultPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 단일 삭제 버튼 방식으로 변경되어 이벤트 핸들러가 필요 없어짐
            // 하지만 다른 속성 변경 시 필요할 수 있으므로 메서드는 유지
        }

        /// <summary>
        /// SheetResults 컬렉션 변경 이벤트를 처리합니다.
        /// </summary>
        private void OnSheetResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (OmrSheetResult item in e.NewItems)
                {
                    item.PropertyChanged += OnSheetResultPropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (OmrSheetResult item in e.OldItems)
                {
                    item.PropertyChanged -= OnSheetResultPropertyChanged;
                }
            }
        }

        /// <summary>
        /// SheetResults 컬렉션의 항목들에 PropertyChanged 이벤트 핸들러를 연결/해제합니다.
        /// </summary>
        private void SubscribeToSheetResults(ObservableCollection<OmrSheetResult>? collection)
        {
            if (collection == null) return;

            // CollectionChanged 이벤트 구독 (항목 추가/제거 시 자동으로 PropertyChanged 구독/해제)
            collection.CollectionChanged += OnSheetResultsCollectionChanged;

            // 기존 항목들에 PropertyChanged 이벤트 구독
            foreach (var item in collection)
            {
                item.PropertyChanged += OnSheetResultPropertyChanged;
            }
        }

        /// <summary>
        /// SheetResults 컬렉션의 항목들에서 PropertyChanged 이벤트 핸들러를 해제합니다.
        /// </summary>
        private void UnsubscribeFromSheetResults(ObservableCollection<OmrSheetResult>? collection)
        {
            if (collection == null) return;

            // CollectionChanged 이벤트 구독 해제
            collection.CollectionChanged -= OnSheetResultsCollectionChanged;

            // 모든 항목에서 PropertyChanged 이벤트 구독 해제
            foreach (var item in collection)
            {
                item.PropertyChanged -= OnSheetResultPropertyChanged;
            }
        }

        /// <summary>
        /// 이미지 ID에 대한 IngestDocState를 가져오거나 생성합니다.
        /// </summary>
        private IngestDocState GetOrCreateIngestState(string imageId)
        {
            lock (_ingestStateLock)
            {
                if (_session.IngestStateByImageId.TryGetValue(imageId, out var state))
                {
                    return state;
                }

                state = new IngestDocState();
                _session.IngestStateByImageId[imageId] = state;
                return state;
            }
        }


        public MarkingViewModel(MarkingDetector markingDetector, NavigationViewModel navigation, Workspace workspace, StateStore stateStore)
        {
            _markingDetector = markingDetector ?? throw new ArgumentNullException(nameof(markingDetector));
            _barcodeReaderService = new BarcodeReaderService();
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _sessionStore = new SessionStore();
            _session = _sessionStore.Load(); // session.json에서 세션 로드
            _imageLoader = new ImageLoader();
            _alignmentService = new ImageAlignmentService();
            _renderer = new Renderer();
            _markingAnalyzer = new MarkingAnalyzer();
            _currentImageDisplayRect = new Rect();

            InitializeCommands();

            // 필터 옵션 초기화
            InitializeFilterOptions();

            // ✅ 중요: 모드 진입 시 전체 문서 정렬을 동기로 수행하면 UI가 멈출 수 있습니다.
            // (특히 aligned_omr가 정리되어 정렬된 이미지가 사라진 경우, 수천 장 재정렬이 발생)
            // 세션 문서/결과는 즉시 바인딩만 하고, 정렬은 폴더 로드/전체 리딩 등의 작업에서(ProgressRunner로) 수행합니다.
            InitializeFromSessionWithoutBlocking();
        }

        public void OnRoundChanged(string? previousRound, string? currentRound)
        {
            Logger.Instance.Info($"회차 변경 감지: {previousRound ?? "(null)"} → {currentRound ?? "(null)"}");
            UiThread.Invoke(ReloadFromSession);
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
                    
                    // 문서 변경 시 마킹/바코드 결과 복원 또는 초기화
                    if (_selectedDocument == null)
                    {
                        CurrentMarkingResults = null;
                        CurrentBarcodeResults = null;
                        DisplayImage = null;
                    }
                    else
                    {
                        // Session.MarkingResults에서 해당 문서의 마킹 결과 복원
                        if (_session.MarkingResults != null && 
                            _session.MarkingResults.TryGetValue(_selectedDocument.ImageId, out var results))
                        {
                            CurrentMarkingResults = results;
                        }
                        else
                        {
                            // 마킹 결과가 없으면 null로 설정 (마킹 리딩 전까지)
                            CurrentMarkingResults = null;
                        }

                        // Session.BarcodeResults에서 해당 문서의 바코드 결과 복원
                        if (_session.BarcodeResults != null && 
                            _session.BarcodeResults.TryGetValue(_selectedDocument.ImageId, out var barcodeResults))
                        {
                            CurrentBarcodeResults = barcodeResults;
                        }
                        else
                        {
                            // 바코드 결과가 없으면 null로 설정
                            CurrentBarcodeResults = null;
                        }

                        // 결과가 없으면 DisplayImage도 null
                        if (CurrentMarkingResults == null && CurrentBarcodeResults == null)
                        {
                            DisplayImage = null;
                        }
                    }
                    
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedDocument));
                    
                    // 마킹 또는 바코드 결과가 있을 때 DisplayImage 생성
                    if (CurrentMarkingResults != null || CurrentBarcodeResults != null)
                    {
                        UpdateDisplayImage();
                    }
                }
            }
        }

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

        public List<BarcodeResult>? CurrentBarcodeResults
        {
            get => _currentBarcodeResults;
            set
            {
                _currentBarcodeResults = value;
                OnPropertyChanged();
                UpdateDisplayImage(); // 바코드 결과 변경 시 이미지 업데이트
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

        public ICommand DetectMarkingsCommand { get; private set; } = null!;
        public ICommand DetectAllMarkingsCommand { get; private set; } = null!;
        public ICommand DetectUnreadMarkingsCommand { get; private set; } = null!;
        public ICommand LoadFolderCommand { get; private set; } = null!;
        public ICommand ExportToXlsxCommand { get; private set; } = null!;
        public ICommand SetFilterModeCommand { get; private set; } = null!;
        public ICommand ResetFiltersCommand { get; private set; } = null!;
        
        /// <summary>
        /// 네비게이션 ViewModel (홈으로 이동 등)
        /// </summary>
        public NavigationViewModel Navigation => _navigation;

        /// <summary>
        /// 모든 문서의 OMR 용지 결과 (문항별 마킹 정리)
        /// </summary>
        public ObservableCollection<OmrSheetResult>? SheetResults
        {
            get => _sheetResults;
            set
            {
                // 기존 컬렉션의 이벤트 구독 해제
                UnsubscribeFromSheetResults(_sheetResults);

                _sheetResults = value;

                // 새 컬렉션의 이벤트 구독
                SubscribeToSheetResults(_sheetResults);

                OnPropertyChanged();
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(DuplicateCount));
                OnPropertyChanged(nameof(NullCombinedIdCount));
                
                // Command의 CanExecute 상태 업데이트
                if (ExportToXlsxCommand is RelayCommand cmd)
                {
                    cmd.RaiseCanExecuteChanged();
                }
            }
        }

        public int ReadyForReadingCount
        {
            get => _readyForReadingCount;
            private set
            {
                if (_readyForReadingCount != value)
                {
                    _readyForReadingCount = value;
                    OnPropertyChanged();
                    if (DetectUnreadMarkingsCommand is RelayCommand cmd)
                    {
                        cmd.RaiseCanExecuteChanged();
                    }
                }
            }
        }

        /// <summary>
        /// 오류가 있는 용지 수
        /// </summary>
        public int ErrorCount => SheetResults?.Count(r => r.HasErrors) ?? 0;

        /// <summary>
        /// ID 중복을 가진 용지 수
        /// </summary>
        public int DuplicateCount => SheetResults?.Count(r => r.IsDuplicate) ?? 0;

        /// <summary>
        /// 결합ID가 없는 용지 수
        /// </summary>
        public int NullCombinedIdCount => SheetResults?.Count(r => string.IsNullOrEmpty(r.CombinedId)) ?? 0;

        /// <summary>
        /// 필터링된 SheetResults (ICollectionView)
        /// </summary>
        public ICollectionView? FilteredSheetResults
        {
            get => _filteredSheetResults;
            private set
            {
                _filteredSheetResults = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 현재 필터 모드 ("All", "Errors", "Duplicates")
        /// </summary>
        public string FilterMode
        {
            get => _filterMode;
            set
            {
                if (_filterMode != value)
                {
                    _filterMode = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// 이미지 표시 영역 (줌 적용 후)
        /// </summary>
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
                }
            }
        }

        /// <summary>
        /// 시각 필터 옵션 (전체, 오전, 오후)
        /// </summary>
        public ObservableCollection<string> SessionFilterOptions
        {
            get => _sessionFilterOptions;
            private set
            {
                _sessionFilterOptions = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 실(면접실) 필터 옵션 (전체 + 동적 추출)
        /// </summary>
        public ObservableCollection<string> RoomFilterOptions
        {
            get => _roomFilterOptions;
            private set
            {
                _roomFilterOptions = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 순 필터 옵션 (전체 + 동적 추출)
        /// </summary>
        public ObservableCollection<string> OrderFilterOptions
        {
            get => _orderFilterOptions;
            private set
            {
                _orderFilterOptions = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 선택된 시각 필터
        /// </summary>
        public string? SelectedSessionFilter
        {
            get => _selectedSessionFilter;
            set
            {
                if (_selectedSessionFilter != value)
                {
                    _selectedSessionFilter = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// 선택된 실(면접실) 필터
        /// </summary>
        public string? SelectedRoomFilter
        {
            get => _selectedRoomFilter;
            set
            {
                if (_selectedRoomFilter != value)
                {
                    _selectedRoomFilter = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// 선택된 순 필터
        /// </summary>
        public string? SelectedOrderFilter
        {
            get => _selectedOrderFilter;
            set
            {
                if (_selectedOrderFilter != value)
                {
                    _selectedOrderFilter = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        /// <summary>
        /// ImageId로 문서를 선택합니다 (OMR 결과 테이블 행 더블 클릭 시 사용).
        /// </summary>
        public void SelectDocumentByImageId(string imageId)
        {
            if (Documents == null) return;

            var document = Documents.FirstOrDefault(d => d.ImageId == imageId);
            if (document != null)
            {
                SelectedDocument = document;
                Logger.Instance.Info($"OMR 결과 테이블에서 문서 선택: {Path.GetFileName(document.SourcePath)}");
            }
            else
            {
                Logger.Instance.Warning($"문서를 찾을 수 없음: ImageId={imageId}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void OnNavigatedTo(object? parameter)
        {
            // 정책: 모드 재진입 시 항상 기본 정렬로 복구
            if (FilteredSheetResults == null) return;

            FilteredSheetResults.SortDescriptions.Clear();

            // 1. IsDuplicate 내림차순 (중복이 먼저)
            FilteredSheetResults.SortDescriptions.Add(
                new SortDescription(nameof(OmrSheetResult.IsDuplicate), ListSortDirection.Descending));

            // 2. IsSimpleError 내림차순 (단순 오류가 그 다음)
            FilteredSheetResults.SortDescriptions.Add(
                new SortDescription(nameof(OmrSheetResult.IsSimpleError), ListSortDirection.Descending));

            // 3. StudentId 오름차순 (수험번호 순)
            FilteredSheetResults.SortDescriptions.Add(
                new SortDescription(nameof(OmrSheetResult.StudentId), ListSortDirection.Ascending));

            // 4. CombinedId 오름차순 (결합ID 순)
            FilteredSheetResults.SortDescriptions.Add(
                new SortDescription(nameof(OmrSheetResult.CombinedId), ListSortDirection.Ascending));

            // 5. ImageFileName 오름차순 (파일명 순)
            FilteredSheetResults.SortDescriptions.Add(
                new SortDescription(nameof(OmrSheetResult.ImageFileName), ListSortDirection.Ascending));

            FilteredSheetResults.Refresh();
        }

        public void OnNavigatedFrom()
        {
            // no-op
        }
    }
}
