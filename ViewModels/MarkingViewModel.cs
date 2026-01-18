using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Utils;
using SimpleOverlayEditor.Views;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 마킹 리딩 전용 ViewModel입니다.
    /// </summary>
    public class MarkingViewModel : INotifyPropertyChanged
    {
        private readonly MarkingDetector _markingDetector;
        private readonly BarcodeReaderService _barcodeReaderService;
        private readonly NavigationViewModel _navigation;
        private readonly Workspace _workspace;
        private readonly StateStore _stateStore;
        private readonly SessionStore _sessionStore;
        private readonly Session _session;
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

            DetectMarkingsCommand = new RelayCommand(
                OnDetectMarkings, 
                () => SelectedDocument != null && ScoringAreas != null && ScoringAreas.Count() == OmrConstants.TotalScoringAreas);
            DetectAllMarkingsCommand = new RelayCommand(
                OnDetectAllMarkings, 
                () => Documents != null && Documents.Count() > 0 && ScoringAreas != null && ScoringAreas.Count() == OmrConstants.TotalScoringAreas);
            LoadFolderCommand = new RelayCommand(OnLoadFolder);
            ExportToCsvCommand = new RelayCommand(
                OnExportToCsv, 
                () => SheetResults != null && SheetResults.Count > 0);
            SetFilterModeCommand = new RelayCommand<string>(OnSetFilterMode);
            
            // 필터 옵션 초기화
            InitializeFilterOptions();

            // ✅ 중요: 모드 진입 시 전체 문서 정렬을 동기로 수행하면 UI가 멈출 수 있습니다.
            // (특히 aligned_cache가 정리되어 정렬된 이미지가 사라진 경우, 수천 장 재정렬이 발생)
            // 세션 문서/결과는 즉시 바인딩만 하고, 정렬은 폴더 로드/전체 리딩 등의 작업에서(ProgressRunner로) 수행합니다.
            InitializeFromSessionWithoutBlocking();
        }

        /// <summary>
        /// 세션에 저장된 문서/결과를 UI에 즉시 반영합니다 (정렬은 수행하지 않음).
        /// </summary>
        private void InitializeFromSessionWithoutBlocking()
        {
            Documents = _session.Documents;
            OnPropertyChanged(nameof(Documents));
            OnPropertyChanged(nameof(DocumentCount));

            Logger.Instance.Info($"Session.Documents 초기화(비차단): {_session.Documents.Count}개 문서 로드됨");

            // 기존 세션에 결과가 있으면 SheetResults 업데이트
            if (_session.MarkingResults != null && _session.MarkingResults.Count > 0)
            {
                UpdateSheetResults();
            }
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

        public ICommand DetectMarkingsCommand { get; }
        public ICommand DetectAllMarkingsCommand { get; }
        public ICommand LoadFolderCommand { get; }
        public ICommand ExportToCsvCommand { get; }
        public ICommand SetFilterModeCommand { get; }
        
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
                if (ExportToCsvCommand is RelayCommand cmd)
                {
                    cmd.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 오류가 있는 용지 수
        /// </summary>
        public int ErrorCount => SheetResults?.Count(r => r.HasErrors) ?? 0;

        /// <summary>
        /// 중복 결합ID를 가진 용지 수
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
        /// 필터 모드를 설정합니다.
        /// </summary>
        private void OnSetFilterMode(string? filterMode)
        {
            if (!string.IsNullOrEmpty(filterMode))
            {
                FilterMode = filterMode;
            }
        }

        /// <summary>
        /// 초기 정렬을 적용합니다 (View 레벨에서 정렬).
        /// 정렬 순서: 중복 데이터 -> 단순 오류 -> 정상 데이터 순서
        /// 각 그룹 내에서는 수험번호 -> 결합ID -> 파일명 순으로 정렬
        /// 
        /// 주의: 이 메서드는 초기 정렬만 설정합니다. 이후 사용자가 열 헤더를 클릭하면
        /// DataGrid가 자동으로 SortDescriptions를 관리하므로, 사용자의 정렬 변경이 누적됩니다.
        /// </summary>
        private void ApplyInitialSort()
        {
            if (FilteredSheetResults == null) return;

            // 기존 정렬이 없을 때만 초기 정렬 설정
            if (FilteredSheetResults.SortDescriptions.Count == 0)
            {
                // 1. IsDuplicate 내림차순 (중복이 먼저)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.IsDuplicate), 
                    System.ComponentModel.ListSortDirection.Descending));
                
                // 2. IsSimpleError 내림차순 (단순 오류가 그 다음)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.IsSimpleError), 
                    System.ComponentModel.ListSortDirection.Descending));
                
                // 3. StudentId 오름차순 (수험번호 순)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.StudentId), 
                    System.ComponentModel.ListSortDirection.Ascending));
                
                // 4. CombinedId 오름차순 (결합ID 순)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.CombinedId), 
                    System.ComponentModel.ListSortDirection.Ascending));
                
                // 5. ImageFileName 오름차순 (파일명 순)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.ImageFileName), 
                    System.ComponentModel.ListSortDirection.Ascending));
            }
        }

        /// <summary>
        /// 필터 옵션을 초기화합니다.
        /// </summary>
        private void InitializeFilterOptions()
        {
            // 공통 유틸 사용: 시각/실/순 기본값
            SessionFilterOptions = OmrFilterUtils.CreateDefaultSessionOptions();
            SelectedSessionFilter = OmrFilterUtils.AllLabel;

            RoomFilterOptions = OmrFilterUtils.CreateDefaultAllOnlyOptions();
            SelectedRoomFilter = OmrFilterUtils.AllLabel;

            OrderFilterOptions = OmrFilterUtils.CreateDefaultAllOnlyOptions();
            SelectedOrderFilter = OmrFilterUtils.AllLabel;
        }

        /// <summary>
        /// 필터 옵션을 데이터에서 동적으로 업데이트합니다.
        /// </summary>
        private void UpdateFilterOptions()
        {
            if (SheetResults == null || SheetResults.Count == 0)
            {
                // 데이터가 없으면 기본값만 유지
                return;
            }

            // 실/순 옵션 동적 업데이트 (공통 유틸)
            OmrFilterUtils.UpdateNumericStringOptions(RoomFilterOptions, SheetResults.Select(r => r.RoomNumber));
            OmrFilterUtils.UpdateNumericStringOptions(OrderFilterOptions, SheetResults.Select(r => r.OrderNumber));

            // 현재 선택값이 유효한지 확인
            var selectedRoom = SelectedRoomFilter;
            OmrFilterUtils.EnsureSelectionIsValid(ref selectedRoom, RoomFilterOptions);
            SelectedRoomFilter = selectedRoom;

            var selectedOrder = SelectedOrderFilter;
            OmrFilterUtils.EnsureSelectionIsValid(ref selectedOrder, OrderFilterOptions);
            SelectedOrderFilter = selectedOrder;
        }

        /// <summary>
        /// 필터를 적용합니다.
        /// </summary>
        private void ApplyFilter()
        {
            if (FilteredSheetResults == null) return;

            FilteredSheetResults.Filter = item =>
            {
                if (item is not OmrSheetResult result) return false;

                // 라디오 필터 (전체/오류만/중복만)
                if (!OmrFilterUtils.PassesBaseFilter(_filterMode, result.IsSimpleError, result.IsDuplicate))
                    return false;

                if (!OmrFilterUtils.PassesSelectionFilter(SelectedSessionFilter, result.Session))
                    return false;
                if (!OmrFilterUtils.PassesSelectionFilter(SelectedRoomFilter, result.RoomNumber))
                    return false;
                if (!OmrFilterUtils.PassesSelectionFilter(SelectedOrderFilter, result.OrderNumber))
                    return false;

                return true;
            };

            FilteredSheetResults.Refresh();
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

        /// <summary>
        /// 현재 선택된 문서의 마킹을 리딩합니다.
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
                Logger.Instance.Info($"마킹 리딩 시작: {SelectedDocument.SourcePath}");
                
                // 마킹 리딩
                var results = _markingDetector.DetectMarkings(
                    SelectedDocument, 
                    ScoringAreas, 
                    MarkingThreshold);

                CurrentMarkingResults = results;

                // Session에 마킹 결과 저장
                if (SelectedDocument != null)
                {
                    if (_session.MarkingResults == null)
                    {
                        _session.MarkingResults = new Dictionary<string, List<MarkingResult>>();
                    }
                    _session.MarkingResults[SelectedDocument.ImageId] = results;
                    _sessionStore.Save(_session); // 세션 저장
                }

                // 바코드 디코딩 (바코드 영역이 있는 경우)
                List<BarcodeResult>? barcodeResults = null;
                if (SelectedDocument != null && 
                    _workspace.Template.BarcodeAreas != null && 
                    _workspace.Template.BarcodeAreas.Count > 0)
                {
                    try
                    {
                        Logger.Instance.Info($"바코드 디코딩 시작: {SelectedDocument.SourcePath}");
                        barcodeResults = _barcodeReaderService.DecodeBarcodes(
                            SelectedDocument,
                            _workspace.Template.BarcodeAreas);

                        CurrentBarcodeResults = barcodeResults;

                        // Session에 바코드 결과 저장
                        if (_session.BarcodeResults == null)
                        {
                            _session.BarcodeResults = new Dictionary<string, List<BarcodeResult>>();
                        }
                        _session.BarcodeResults[SelectedDocument.ImageId] = barcodeResults;
                        _sessionStore.Save(_session); // 세션 저장

                        if (barcodeResults != null)
                        {
                            var successCount = barcodeResults.Count(r => r.Success);
                            Logger.Instance.Info($"바코드 디코딩 완료: 총 {barcodeResults.Count}개 영역 중 {successCount}개 성공");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("바코드 디코딩 실패", ex);
                        // 바코드 디코딩 실패해도 마킹 리딩은 완료되었으므로 계속 진행
                    }
                }

                // 오버레이 이미지 생성 및 표시
                UpdateDisplayImage();

                // 결과 이미지 파일 저장
                if (SelectedDocument != null)
                {
                    try
                    {
                        _renderer.RenderSingleDocument(SelectedDocument, _session, _workspace);
                        Logger.Instance.Info($"결과 이미지 파일 저장 완료: {SelectedDocument.SourcePath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("결과 이미지 파일 저장 실패", ex);
                        // 저장 실패해도 마킹 리딩은 완료되었으므로 계속 진행
                    }
                }

                var markedCount = results.Count(r => r.IsMarked);
                var message = $"마킹 리딩 완료\n\n" +
                             $"총 영역: {results.Count}개\n" +
                             $"마킹 리딩: {markedCount}개\n" +
                             $"미마킹: {results.Count - markedCount}개\n\n" +
                             $"임계값: {MarkingThreshold}";
                
                // 바코드 결과 추가
                if (barcodeResults != null && barcodeResults.Count > 0)
                {
                    var barcodeSuccessCount = barcodeResults.Count(r => r.Success);
                    message += $"\n\n바코드 디코딩:\n" +
                              $"총 영역: {barcodeResults.Count}개\n" +
                              $"성공: {barcodeSuccessCount}개\n" +
                              $"실패: {barcodeResults.Count - barcodeSuccessCount}개";
                }

                message += "\n\n결과 이미지가 저장되었습니다.";

                Logger.Instance.Info($"마킹 리딩 완료: {markedCount}/{results.Count}개 마킹 리딩");
                MessageBox.Show(message, "마킹 리딩 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                // OMR 결과 업데이트
                UpdateSheetResults();
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("마킹 리딩 실패", ex);
                MessageBox.Show($"마킹 리딩 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모든 문서의 마킹을 리딩합니다.
        /// </summary>
        private async void OnDetectAllMarkings()
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
                Dictionary<string, List<MarkingResult>>? allResults = null;
                Dictionary<string, List<BarcodeResult>>? allBarcodeResults = null;

                var cancelled = await ProgressRunner.RunAsync(
                    Application.Current.MainWindow,
                    async scope =>
                    {
                        var cancellationToken = scope.CancellationToken;
                        var documentsList = Documents.ToList();
                        Logger.Instance.Info($"전체 문서 바코드/마킹 리딩 시작: {documentsList.Count}개 문서");

                        // 바코드 디코딩 (바코드 영역이 있는 경우) - 마킹 리딩보다 먼저 실행
                        if (_workspace.Template.BarcodeAreas != null && _workspace.Template.BarcodeAreas.Count > 0)
                        {
                            try
                            {
                                Logger.Instance.Info("전체 문서 바코드 디코딩 시작");
                                scope.Report(0, documentsList.Count, "바코드 디코딩 시작");

                                cancellationToken.ThrowIfCancellationRequested();

                                allBarcodeResults = new Dictionary<string, List<BarcodeResult>>();
                                int barcodeIndex = 0;
                                foreach (var document in documentsList)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    barcodeIndex++;
                                    var fileName = Path.GetFileName(document.SourcePath);
                                    scope.Report(barcodeIndex, documentsList.Count, $"바코드 디코딩 중: {fileName}");

                                    try
                                    {
                                        var barcodeResults = _barcodeReaderService.DecodeBarcodes(document, _workspace.Template.BarcodeAreas);
                                        allBarcodeResults[document.ImageId] = barcodeResults;
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Instance.Warning($"바코드 디코딩 실패: {document.SourcePath}, {ex.Message}");
                                        allBarcodeResults[document.ImageId] = new List<BarcodeResult>();
                                    }
                                }

                                cancellationToken.ThrowIfCancellationRequested();

                                scope.Ui(() =>
                                {
                                    _session.BarcodeResults = allBarcodeResults;
                                });

                                Logger.Instance.Info("전체 문서 바코드 디코딩 완료");
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                Logger.Instance.Error("전체 문서 바코드 디코딩 실패", ex);
                                // 바코드 디코딩 실패해도 마킹 리딩은 계속 진행
                            }
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        scope.Report(0, documentsList.Count, "마킹 리딩 시작");

                        cancellationToken.ThrowIfCancellationRequested();

                        // 마킹 리딩
                        allResults = _markingDetector.DetectAllMarkings(
                            documentsList,
                            _workspace.Template,
                            MarkingThreshold,
                            (current, total, message) =>
                            {
                                if (cancellationToken.IsCancellationRequested) return;
                                scope.Report(current, total, message);
                            },
                            cancellationToken);

                        cancellationToken.ThrowIfCancellationRequested();

                        scope.Ui(() =>
                        {
                            _session.MarkingResults = allResults;
                            _sessionStore.Save(_session);
                        });

                        await Task.CompletedTask;
                    },
                    title: "진행 중...",
                    initialStatus: "처리 중...");

                if (cancelled)
                {
                    Logger.Instance.Info("전체 마킹 리딩이 취소되었습니다.");
                    MessageBox.Show("작업이 취소되었습니다.", "취소됨", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (allResults == null)
                {
                    throw new InvalidOperationException("마킹 리딩 결과가 생성되지 않았습니다.");
                }

                // 완료 메시지 구성/표시 (UI 스레드)
                int totalDocuments = allResults.Count;
                int totalAreas = 0;
                int totalMarked = 0;
                foreach (var kvp in allResults)
                {
                    totalAreas += kvp.Value.Count;
                    totalMarked += kvp.Value.Count(r => r.IsMarked);
                }

                var message = $"전체 문서 마킹 리딩 완료\n\n" +
                              $"처리된 문서: {totalDocuments}개\n" +
                              $"총 영역: {totalAreas}개\n" +
                              $"마킹 리딩: {totalMarked}개\n" +
                              $"미마킹: {totalAreas - totalMarked}개\n\n" +
                              $"임계값: {MarkingThreshold}";

                if (allBarcodeResults != null && allBarcodeResults.Count > 0)
                {
                    int totalBarcodeAreas = 0;
                    int totalBarcodeSuccess = 0;
                    foreach (var kvp in allBarcodeResults)
                    {
                        totalBarcodeAreas += kvp.Value.Count;
                        totalBarcodeSuccess += kvp.Value.Count(r => r.Success);
                    }
                    message += $"\n\n바코드 디코딩:\n" +
                               $"총 영역: {totalBarcodeAreas}개\n" +
                               $"성공: {totalBarcodeSuccess}개\n" +
                               $"실패: {totalBarcodeAreas - totalBarcodeSuccess}개";
                }

                Logger.Instance.Info($"전체 문서 마킹 리딩 완료: {totalMarked}/{totalAreas}개 마킹 리딩");

                // 현재 선택된 문서의 결과 표시
                if (SelectedDocument != null)
                {
                    if (allResults.TryGetValue(SelectedDocument.ImageId, out var currentResults))
                    {
                        CurrentMarkingResults = currentResults;
                    }

                    if (allBarcodeResults != null &&
                        allBarcodeResults.TryGetValue(SelectedDocument.ImageId, out var currentBarcodeResults))
                    {
                        CurrentBarcodeResults = currentBarcodeResults;
                    }

                    UpdateDisplayImage();
                }

                // OMR 결과 업데이트 및 통계 표시
                UpdateSheetResults();

                if (SheetResults != null && SheetResults.Count > 0)
                {
                    message += $"\n\n결과 분석:\n" +
                              $"총 용지: {SheetResults.Count}개\n" +
                              $"오류 있는 용지: {ErrorCount}개";

                    if (DuplicateCount > 0)
                    {
                        message += $"\n중복 결합ID: {DuplicateCount}개";
                    }

                    if (NullCombinedIdCount > 0)
                    {
                        message += $"\n결합ID 없음: {NullCombinedIdCount}개";
                    }
                }

                MessageBox.Show(message, "전체 마킹 리딩 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                // 모든 문서의 결과 이미지 파일 저장 (백그라운드)
                _ = Task.Run(() =>
                {
                    try
                    {
                        _renderer.RenderAll(_session, _workspace);
                        Logger.Instance.Info("전체 결과 이미지 파일 저장 완료");
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("전체 결과 이미지 파일 저장 실패", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("전체 마킹 리딩 실패", ex);
                MessageBox.Show($"전체 마킹 리딩 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 폴더에서 이미지를 로드합니다.
        /// </summary>
        private async void OnLoadFolder()
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

                    try
                    {
                        List<ImageDocument>? loadedDocuments = null;

                        var cancelled = await ProgressRunner.RunAsync(
                            Application.Current.MainWindow,
                            async scope =>
                            {
                                var cancellationToken = scope.CancellationToken;

                                Logger.Instance.Debug("이미지 파일 로드 시작");
                                loadedDocuments = _imageLoader.LoadImagesFromFolder(
                                    folderPath,
                                    (current, total, message) =>
                                    {
                                        if (cancellationToken.IsCancellationRequested) return;
                                        scope.Report(current, total, message);
                                    },
                                    cancellationToken);

                                cancellationToken.ThrowIfCancellationRequested();

                                Logger.Instance.Info($"이미지 파일 로드 완료. 문서 수: {loadedDocuments.Count}");

                                scope.Report(0, loadedDocuments.Count, "정렬 작업 시작");

                                if (loadedDocuments.Count == 0)
                                {
                                    scope.Ui(() =>
                                    {
                                        Logger.Instance.Warning("선택한 폴더에 이미지 파일이 없음");
                                        MessageBox.Show("선택한 폴더에 이미지 파일이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                                    });
                                    return;
                                }

                                scope.Ui(() =>
                                {
                                    SelectedDocument = null;
                                    _session.Documents.Clear();
                                    _session.MarkingResults.Clear();
                                    _session.BarcodeResults.Clear();
                                    _workspace.InputFolderPath = folderPath;
                                });

                                Logger.Instance.Debug($"문서 {loadedDocuments.Count}개 추가 및 정렬 적용 시작 (병렬 처리)");

                                var completedCount = 0;
                                var lockObject = new object();

                                Parallel.ForEach(loadedDocuments, new ParallelOptions
                                {
                                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                                    CancellationToken = cancellationToken
                                }, doc =>
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    // 이미지 정렬 적용
                                    ApplyAlignmentToDocument(doc);

                                    int current;
                                    lock (lockObject)
                                    {
                                        completedCount++;
                                        current = completedCount;
                                    }

                                    scope.Ui(() =>
                                    {
                                        if (cancellationToken.IsCancellationRequested) return;

                                        var fileName = Path.GetFileName(doc.SourcePath);
                                        scope.Report(current, loadedDocuments.Count, $"정렬 중: {fileName}");
                                        _session.Documents.Add(doc);
                                    });
                                });

                                cancellationToken.ThrowIfCancellationRequested();

                                scope.Ui(() =>
                                {
                                    _stateStore.SaveWorkspaceState(_workspace);
                                    _sessionStore.Save(_session);
                                });

                                await Task.CompletedTask;
                            },
                            title: "진행 중...",
                            initialStatus: "처리 중...");

                        if (cancelled)
                        {
                            Logger.Instance.Info("폴더 로드/정렬 작업이 취소되었습니다.");
                            MessageBox.Show("작업이 취소되었습니다.", "취소됨", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        if (loadedDocuments == null || loadedDocuments.Count == 0)
                        {
                            return;
                        }

                        // UI 반영
                        SelectedDocument = null;
                        _workspace.SelectedDocumentId = null;
                        CurrentMarkingResults = null;
                        CurrentBarcodeResults = null;
                        DisplayImage = null;

                        Documents = _session.Documents;
                        OnPropertyChanged(nameof(Documents));
                        OnPropertyChanged(nameof(DocumentCount));

                        Logger.Instance.Info($"폴더 로드 완료. 총 {loadedDocuments.Count}개 이미지 로드됨");
                        MessageBox.Show($"{loadedDocuments.Count}개의 이미지를 로드했습니다.", "로드 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("폴더 로드 실패", ex);
                        MessageBox.Show($"폴더 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
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
            BitmapImage? bitmap = null;
            AlignmentResult? result = null;
            
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
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(document.SourcePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                // 정렬 적용
                result = _alignmentService.AlignImage(bitmap, _workspace.Template);

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

                    // 정렬된 이미지 크기 저장 (저장 전에)
                    var alignedImageWidth = result.AlignedImage.PixelWidth;
                    var alignedImageHeight = result.AlignedImage.PixelHeight;

                    // 정렬된 이미지를 캐시에 저장
                    var alignedImagePath = SaveAlignedImageToCache(document, result.AlignedImage);
                    document.AlignmentInfo.AlignedImagePath = alignedImagePath;

                    // 정렬된 이미지 크기로 ImageWidth/Height 업데이트
                    document.ImageWidth = alignedImageWidth;
                    document.ImageHeight = alignedImageHeight;

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
            finally
            {
                // 메모리 최적화: 이미지 처리 후 즉시 참조 해제 (GC가 회수할 수 있도록)
                if (result != null && result.AlignedImage != null)
                {
                    // 정렬된 이미지는 디스크에 저장되었으므로 메모리에서 해제 가능
                    result = null;
                }
                bitmap = null;
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
        /// 이미지 표시 영역을 업데이트합니다 (줌 레벨 적용).
        /// </summary>
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
                    // 마킹 리딩 결과가 있으면 결과에 따라 색상 변경
                    var scoringAreas = template.ScoringAreas.ToList();
                    for (int i = 0; i < scoringAreas.Count; i++)
                    {
                        var overlay = scoringAreas[i];
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        
                        // 마킹 리딩 결과 확인
                        Brush? fillBrush = null;
                        Pen? pen = null;
                        
                        if (CurrentMarkingResults != null && i < CurrentMarkingResults.Count)
                        {
                            var result = CurrentMarkingResults[i];
                            if (result.IsMarked)
                            {
                                // 마킹 리딩: 파란색 반투명 채우기 + 빨간색 테두리
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
                            // 마킹 리딩 결과 없음: 빨간색 테두리만
                            pen = new Pen(Brushes.Red, 2.0);
                        }
                        
                        drawingContext.DrawRectangle(fillBrush, pen, rect);
                    }

                    // 템플릿의 바코드 영역 그리기
                    var barcodeAreas = template.BarcodeAreas.ToList();
                    for (int i = 0; i < barcodeAreas.Count; i++)
                    {
                        var overlay = barcodeAreas[i];
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        
                        // 바코드 디코딩 결과 확인
                        Brush? fillBrush = null;
                        Pen? pen = null;
                        
                        if (CurrentBarcodeResults != null && i < CurrentBarcodeResults.Count)
                        {
                            var result = CurrentBarcodeResults[i];
                            if (result.Success)
                            {
                                // 바코드 디코딩 성공: 주황색 반투명 채우기 + 주황색 테두리
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 255, 165, 0));
                                pen = new Pen(Brushes.Orange, 2.0);
                            }
                            else
                            {
                                // 바코드 디코딩 실패: 회색 반투명 채우기 + 회색 테두리
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 128, 128, 128));
                                pen = new Pen(Brushes.Gray, 2.0);
                            }
                        }
                        else
                        {
                            // 바코드 디코딩 결과 없음: 주황색 테두리만
                            pen = new Pen(Brushes.Orange, 2.0);
                        }
                        
                        drawingContext.DrawRectangle(fillBrush, pen, rect);

                        // 바코드 디코딩 성공 시 텍스트 표시
                        if (CurrentBarcodeResults != null && i < CurrentBarcodeResults.Count)
                        {
                            var result = CurrentBarcodeResults[i];
                            if (result.Success && !string.IsNullOrEmpty(result.DecodedText))
                            {
                                var text = result.DecodedText;
                                var formattedText = new FormattedText(
                                    text,
                                    System.Globalization.CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight,
                                    new Typeface("Arial"),
                                    12,
                                    Brushes.White,
                                    96.0);

                                // 텍스트 배경 (검은색 반투명)
                                var textRect = new Rect(
                                    overlay.X,
                                    overlay.Y - formattedText.Height - 2,
                                    Math.Max(formattedText.Width + 4, overlay.Width),
                                    formattedText.Height + 2);
                                drawingContext.DrawRectangle(
                                    new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                                    null,
                                    textRect);

                                // 텍스트 그리기
                                drawingContext.DrawText(
                                    formattedText,
                                    new Point(overlay.X + 2, overlay.Y - formattedText.Height));
                            }
                        }
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

        /// <summary>
        /// 마킹 결과를 OMR 용지 구조에 맞게 분석하여 SheetResults를 업데이트합니다.
        /// </summary>
        private void UpdateSheetResults()
        {
            if (_session.Documents == null || _session.Documents.Count == 0)
            {
                // SheetResults가 있으면 Clear만, 없으면 null로 설정
                if (SheetResults != null)
                {
                    SheetResults.Clear();
                }
                else
                {
                    SheetResults = null;
                    FilteredSheetResults = null;
                }
                OnPropertyChanged(nameof(FilteredSheetResults));
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(DuplicateCount));
                OnPropertyChanged(nameof(NullCombinedIdCount));
                
                // 필터 옵션 초기화 (데이터 없음)
                UpdateFilterOptions();
                return;
            }

            try
            {
                var results = _markingAnalyzer.AnalyzeAllSheets(_session);
                
                // 결합ID 기준으로 중복 검출
                var groupedByCombinedId = DuplicateDetector.DetectAndApplyCombinedIdDuplicates(results);
                
                // 컬렉션 인스턴스 유지: 기존 SheetResults가 있으면 Clear 후 다시 채우기
                // 이렇게 하면 FilteredSheetResults도 유지되어 사용자 정렬 상태가 보존됨
                if (SheetResults == null)
                {
                    // 처음 생성하는 경우만 새로운 ObservableCollection 생성
                    SheetResults = new ObservableCollection<OmrSheetResult>(results);
                    
                    // CollectionViewSource 생성 및 정렬/필터 설정
                    FilteredSheetResults = CollectionViewSource.GetDefaultView(SheetResults);
                    ApplyInitialSort();  // 초기 정렬 적용 (기존 정렬이 없을 때만)
                    ApplyFilter();  // 필터 적용
                }
                else
                {
                    // 기존 컬렉션 인스턴스 유지: 사용자 정렬 상태 보존을 위해
                    // CollectionViewSource.GetDefaultView는 같은 ObservableCollection에 대해 같은 ICollectionView를 반환하므로
                    // FilteredSheetResults도 유지됨
                    
                    SheetResults.Clear();
                    foreach (var item in results)
                    {
                        SheetResults.Add(item);
                    }
                    
                    // FilteredSheetResults는 재할당할 필요 없음 (같은 인스턴스이므로)
                    // 다만, 필터는 재적용 필요
                    ApplyFilter();
                }
                
                OnPropertyChanged(nameof(SheetResults));
                OnPropertyChanged(nameof(FilteredSheetResults));
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(DuplicateCount));
                OnPropertyChanged(nameof(NullCombinedIdCount));
                
                // 필터 옵션 업데이트 (실/순 필터 동적 추출)
                UpdateFilterOptions();
                
                var duplicateRowCount = groupedByCombinedId.Values.SelectMany(g => g).Count();
                if (duplicateRowCount > 0)
                {
                    Logger.Instance.Warning($"중복 결합ID 검출: {duplicateRowCount}개 행");
                }
                else
                {
                    Logger.Instance.Info($"중복 결합ID 없음");
                }
                
                Logger.Instance.Info($"OMR 결과 업데이트 완료: {results.Count}개 용지");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("OMR 결과 업데이트 실패", ex);
                SheetResults = null;
                FilteredSheetResults = null;
                OnPropertyChanged(nameof(FilteredSheetResults));
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(DuplicateCount));
                OnPropertyChanged(nameof(NullCombinedIdCount));
            }
        }

        /// <summary>
        /// 단일 항목을 삭제합니다.
        /// </summary>
        public void DeleteSingleItem(string imageId)
        {
            if (SheetResults == null || string.IsNullOrEmpty(imageId)) return;
            
            var itemToDelete = SheetResults.FirstOrDefault(r => r.ImageId == imageId);
            if (itemToDelete == null) return;
            
            // 확인 다이얼로그
            var message = $"'{itemToDelete.ImageFileName}' 항목을 삭제하시겠습니까?\n\n" +
                         "이 작업은 되돌릴 수 없습니다.";
            
            var result = MessageBox.Show(message, "삭제 확인", 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes) return;
            
            // 중복 그룹 검증 (단일 삭제이므로 단순화)
            if (itemToDelete.IsDuplicate && !string.IsNullOrEmpty(itemToDelete.CombinedId))
            {
                var allInGroup = SheetResults
                    .Where(r => r.CombinedId == itemToDelete.CombinedId)
                    .ToList();
                
                if (allInGroup.Count > 1)
                {
                    // 같은 그룹에 다른 항목이 있으면 삭제 가능 (단일 항목만 삭제)
                    // 경고 없이 진행 (사용자가 명시적으로 선택했으므로)
                }
            }
            
            // Session에서 삭제
            DeleteDocumentsFromSession(new[] { imageId });
            
            // Session 저장
            _sessionStore.Save(_session);
            
            // SheetResults 재생성
            UpdateSheetResults();
            
            Logger.Instance.Info($"단일 항목 삭제 완료: {imageId}");
        }

        /// <summary>
        /// Session에서 지정된 ImageId들을 삭제합니다.
        /// </summary>
        private void DeleteDocumentsFromSession(IEnumerable<string> imageIdsToDelete)
        {
            var imageIdSet = imageIdsToDelete.ToHashSet();
            
            // 1. Documents에서 제거
            var documentsToRemove = _session.Documents
                .Where(d => imageIdSet.Contains(d.ImageId))
                .ToList();
            
            foreach (var doc in documentsToRemove)
            {
                _session.Documents.Remove(doc);
                Logger.Instance.Info($"Document 삭제: {doc.SourcePath} (ImageId: {doc.ImageId})");
            }
            
            // 2. MarkingResults에서 제거
            if (_session.MarkingResults != null)
            {
                foreach (var imageId in imageIdSet)
                {
                    if (_session.MarkingResults.Remove(imageId))
                    {
                        Logger.Instance.Info($"MarkingResults 삭제: ImageId={imageId}");
                    }
                }
            }
            
            // 3. BarcodeResults에서 제거
            if (_session.BarcodeResults != null)
            {
                foreach (var imageId in imageIdSet)
                {
                    if (_session.BarcodeResults.Remove(imageId))
                    {
                        Logger.Instance.Info($"BarcodeResults 삭제: ImageId={imageId}");
                    }
                }
            }
        }

        /// <summary>
        /// CSV 파일로 내보냅니다.
        /// </summary>
        private void OnExportToCsv()
        {
            if (SheetResults == null || SheetResults.Count == 0)
            {
                MessageBox.Show("내보낼 데이터가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*",
                    FileName = $"OMR_Results_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    ExportToCsv(dialog.FileName, SheetResults);
                    MessageBox.Show("CSV 파일로 저장되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("CSV 내보내기 실패", ex);
                MessageBox.Show($"CSV 내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// OMR 결과를 CSV 형식으로 저장합니다.
        /// </summary>
        private void ExportToCsv(string filePath, IEnumerable<OmrSheetResult> results)
        {
            using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
            
            // 헤더 작성 (UTF-8 BOM 추가)
            writer.Write('\uFEFF'); // UTF-8 BOM
            writer.WriteLine("파일명,수험번호,시각,실,순,면접번호,결합ID,문항1,문항2,문항3,문항4,오류,오류메시지");
            
            // 데이터 작성
            foreach (var result in results)
            {
                writer.WriteLine($"{EscapeCsvField(result.ImageFileName)}," +
                                $"{EscapeCsvField(result.StudentId ?? "")}," +
                                $"{EscapeCsvField(result.Session ?? "")}," +
                                $"{EscapeCsvField(result.RoomNumber ?? "")}," +
                                $"{EscapeCsvField(result.OrderNumber ?? "")}," +
                                $"{EscapeCsvField(result.InterviewId ?? "")}," +
                                $"{EscapeCsvField(result.CombinedId ?? "")}," +
                                $"{(result.Question1Marking?.ToString() ?? "")}," +
                                $"{(result.Question2Marking?.ToString() ?? "")}," +
                                $"{(result.Question3Marking?.ToString() ?? "")}," +
                                $"{(result.Question4Marking?.ToString() ?? "")}," +
                                $"{(result.HasErrors ? "예" : "아니오")}," +
                                $"{EscapeCsvField(result.ErrorMessage ?? "")}");
            }
        }

        /// <summary>
        /// CSV 필드를 이스케이프합니다 (쉼표, 따옴표, 개행 포함 시)
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // 쉼표, 따옴표, 개행이 있으면 따옴표로 감싸고 따옴표는 두 개로 변환
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }

            return field;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

