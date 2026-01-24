using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 수기 검산 화면용 ViewModel입니다.
    /// </summary>
    public class ManualVerificationViewModel : INotifyPropertyChanged
    {
        private readonly NavigationViewModel _navigation;
        private readonly Workspace _workspace;
        private readonly SessionStore _sessionStore;
        private readonly ScoringRuleStore _scoringRuleStore;
        private readonly MarkingAnalyzer _markingAnalyzer;

        private Session? _session;
        private ScoringRule? _scoringRule;
        private Dictionary<string, ImageDocument> _documentByImageId = new();
        private List<OmrSheetResult> _allSheetResults = new();
        private Dictionary<string, GradingResult> _gradingByStudentId = new();
        private int _sampleSeed;

        private bool _isBusy;
        private string? _busyMessage;
        private StudentSampleItem? _selectedStudent;
        private ObservableCollection<OmrSheetResult> _studentSheets = new();
        private OmrSheetResult? _selectedSheet;
        private ImageDocument? _selectedDocument;
        private BitmapSource? _displayImage;
        private ObservableCollection<QuestionVerificationRow> _questionRows = new();
        private GradingResult? _selectedStudentGradingResult;
        private Rect _currentImageDisplayRect;
        private double _zoomLevel = 1.0;

        public ManualVerificationViewModel(NavigationViewModel navigation, Workspace workspace)
        {
            _navigation = navigation ?? throw new System.ArgumentNullException(nameof(navigation));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

            _sessionStore = new SessionStore();
            _scoringRuleStore = new ScoringRuleStore();
            _markingAnalyzer = new MarkingAnalyzer();
            
            NavigateToHomeCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(ApplicationMode.Home);
            });

            RegenerateSampleCommand = new RelayCommand(RegenerateSample, () => !IsBusy);
            PreviousImageCommand = new RelayCommand(SelectPreviousImage, () => !IsBusy && StudentSheets.Count > 0 && SelectedSheet != null);
            NextImageCommand = new RelayCommand(SelectNextImage, () => !IsBusy && StudentSheets.Count > 0 && SelectedSheet != null);
            ReloadCommand = new RelayCommand(() => _ = ReloadAsync(), () => !IsBusy);

            // 기본 seed: 매 진입마다 고정되지 않게
            _sampleSeed = Environment.TickCount;

            // 초기 로드 (UI 블로킹 방지)
            _ = ReloadAsync();
        }

        /// <summary>
        /// 홈 화면으로 이동
        /// </summary>
        public ICommand NavigateToHomeCommand { get; }
        public ICommand RegenerateSampleCommand { get; }
        public ICommand PreviousImageCommand { get; }
        public ICommand NextImageCommand { get; }
        public ICommand ReloadCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public string? BusyMessage
        {
            get => _busyMessage;
            private set
            {
                if (_busyMessage != value)
                {
                    _busyMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public int SampleSeed
        {
            get => _sampleSeed;
            private set
            {
                if (_sampleSeed != value)
                {
                    _sampleSeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<StudentSampleItem> SampledStudents { get; } = new();

        public StudentSampleItem? SelectedStudent
        {
            get => _selectedStudent;
            set
            {
                if (!ReferenceEquals(_selectedStudent, value))
                {
                    _selectedStudent = value;
                    OnPropertyChanged();
                    UpdateStudentSheets();
                    UpdateStudentGradingResult();
                }
            }
        }

        public ObservableCollection<OmrSheetResult> StudentSheets
        {
            get => _studentSheets;
            private set
            {
                _studentSheets = value ?? new ObservableCollection<OmrSheetResult>();
                OnPropertyChanged();
            }
        }

        public OmrSheetResult? SelectedSheet
        {
            get => _selectedSheet;
            set
            {
                if (!ReferenceEquals(_selectedSheet, value))
                {
                    _selectedSheet = value;
                    OnPropertyChanged();
                    UpdateSelectedSheetDerivedState();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public ImageDocument? SelectedDocument
        {
            get => _selectedDocument;
            private set
            {
                if (!ReferenceEquals(_selectedDocument, value))
                {
                    _selectedDocument = value;
                    OnPropertyChanged();
                }
            }
        }

        public BitmapSource? DisplayImage
        {
            get => _displayImage;
            private set
            {
                if (!ReferenceEquals(_displayImage, value))
                {
                    _displayImage = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<QuestionVerificationRow> QuestionRows
        {
            get => _questionRows;
            private set
            {
                _questionRows = value ?? new ObservableCollection<QuestionVerificationRow>();
                OnPropertyChanged();
            }
        }

        public GradingResult? SelectedStudentGradingResult
        {
            get => _selectedStudentGradingResult;
            private set
            {
                if (!ReferenceEquals(_selectedStudentGradingResult, value))
                {
                    _selectedStudentGradingResult = value;
                    OnPropertyChanged();
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

        public void UpdateImageDisplayRect(Size availableSize)
        {
            if (SelectedDocument == null) return;

            var baseRect = ZoomHelper.CalculateImageDisplayRect(
                SelectedDocument.ImageWidth,
                SelectedDocument.ImageHeight,
                availableSize,
                ZoomHelper.ImageAlignment.TopLeft);

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

        private async Task ReloadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            BusyMessage = "수기 검산 데이터 로드 중...";

            try
            {
                var loadResult = await Task.Run(() => LoadCore());

                UiThread.Invoke(() =>
                {
                    _session = loadResult.Session;
                    _scoringRule = loadResult.ScoringRule;
                    _allSheetResults = loadResult.SheetResults;
                    _documentByImageId = loadResult.DocumentByImageId;
                    _gradingByStudentId = loadResult.GradingByStudentId;

                    ApplySampling(seed: SampleSeed);
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("수기 검산 로드 실패", ex);
                UiThread.Invoke(() =>
                {
                    SampledStudents.Clear();
                    StudentSheets = new ObservableCollection<OmrSheetResult>();
                    SelectedStudent = null;
                    SelectedSheet = null;
                    SelectedStudentGradingResult = null;
                    SelectedDocument = null;
                    DisplayImage = null;
                    QuestionRows = new ObservableCollection<QuestionVerificationRow>();
                });
            }
            finally
            {
                UiThread.Invoke(() =>
                {
                    BusyMessage = null;
                    IsBusy = false;
                });
            }
        }

        private record LoadResult(
            Session Session,
            ScoringRule ScoringRule,
            List<OmrSheetResult> SheetResults,
            Dictionary<string, ImageDocument> DocumentByImageId,
            Dictionary<string, GradingResult> GradingByStudentId);

        private LoadResult LoadCore()
        {
            var session = _sessionStore.Load();
            var scoringRule = _scoringRuleStore.LoadScoringRule();

            var sheetResults = _markingAnalyzer.AnalyzeAllSheets(session);

            var docById = session.Documents
                .Where(d => !string.IsNullOrEmpty(d.ImageId))
                .GroupBy(d => d.ImageId)
                .ToDictionary(g => g.Key, g => g.First());

            // Headless GradingViewModel 생성 (UI 전환 없이 성적처리 결과 획득)
            // NOTE: 생성자가 내부에서 LoadGradingData() 수행
            var gradingVm = new GradingViewModel(_navigation);
            var gradingByStudentId = new Dictionary<string, GradingResult>();
            if (gradingVm.GradingResults != null)
            {
                foreach (var r in gradingVm.GradingResults)
                {
                    if (!string.IsNullOrEmpty(r.StudentId))
                    {
                        gradingByStudentId[r.StudentId!] = r;
                    }
                }
            }

            return new LoadResult(session, scoringRule, sheetResults, docById, gradingByStudentId);
        }

        private void RegenerateSample()
        {
            SampleSeed = Environment.TickCount;
            ApplySampling(seed: SampleSeed);
        }

        private void ApplySampling(int seed)
        {
            SampledStudents.Clear();

            var studentIds = _allSheetResults
                .Where(r => !string.IsNullOrEmpty(r.StudentId))
                .Select(r => r.StudentId!)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (studentIds.Count == 0)
            {
                SelectedStudent = null;
                StudentSheets = new ObservableCollection<OmrSheetResult>();
                SelectedSheet = null;
                SelectedStudentGradingResult = null;
                SelectedDocument = null;
                DisplayImage = null;
                QuestionRows = new ObservableCollection<QuestionVerificationRow>();
                return;
            }

            var rng = new Random(seed);
            var sampled = studentIds
                .OrderBy(_ => rng.Next())
                .Take(20)
                .ToList();

            // ✅ 랜덤 "선정"은 유지하되, UI에서는 오름차순으로 표시
            var sampledSorted = sampled
                .OrderBy(id => TryParseIntOrMax(id))
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList();

            foreach (var studentId in sampledSorted)
            {
                var imageCount = _allSheetResults.Count(r => r.StudentId == studentId);
                var hasGrading = _gradingByStudentId.ContainsKey(studentId);
                SampledStudents.Add(new StudentSampleItem(studentId, imageCount, hasGrading));
            }

            SelectedStudent = SampledStudents.FirstOrDefault();
        }

        private void UpdateStudentSheets()
        {
            StudentSheets.Clear();

            if (SelectedStudent == null)
            {
                SelectedSheet = null;
                SelectedDocument = null;
                DisplayImage = null;
                QuestionRows = new ObservableCollection<QuestionVerificationRow>();
                return;
            }

            var studentId = SelectedStudent.StudentId;
            var sheets = _allSheetResults
                .Where(r => r.StudentId == studentId)
                // ✅ 같은 수험번호 내 이미지도 오름차순 정렬 (면접번호 숫자 정렬 우선)
                .OrderBy(r => TryParseIntOrMax(r.InterviewId))
                .ThenBy(r => r.InterviewId ?? "", StringComparer.Ordinal)
                .ThenBy(r => r.ImageFileName ?? "", StringComparer.Ordinal)
                .ToList();

            StudentSheets = new ObservableCollection<OmrSheetResult>(sheets);
            SelectedSheet = StudentSheets.FirstOrDefault();
        }

        private static int TryParseIntOrMax(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return int.MaxValue;
            return int.TryParse(s.Trim(), out var n) ? n : int.MaxValue;
        }

        private void UpdateStudentGradingResult()
        {
            if (SelectedStudent == null)
            {
                SelectedStudentGradingResult = null;
                return;
            }

            SelectedStudentGradingResult = _gradingByStudentId.TryGetValue(SelectedStudent.StudentId, out var result)
                ? result
                : null;
        }

        private void UpdateSelectedSheetDerivedState()
        {
            if (SelectedSheet == null || _session == null)
            {
                SelectedDocument = null;
                DisplayImage = null;
                QuestionRows = new ObservableCollection<QuestionVerificationRow>();
                return;
            }

            if (_documentByImageId.TryGetValue(SelectedSheet.ImageId, out var doc))
            {
                SelectedDocument = doc;
            }
            else
            {
                SelectedDocument = null;
            }

            _session.MarkingResults.TryGetValue(SelectedSheet.ImageId, out var markingResults);
            _session.BarcodeResults.TryGetValue(SelectedSheet.ImageId, out var barcodeResults);

            // 오버레이 렌더링 이미지 갱신 (재리딩 없이 가능)
            DisplayImage = (SelectedDocument != null)
                ? RenderOverlayImage(SelectedDocument, markingResults, barcodeResults)
                : null;

            // 문항별 검산 표 구성
            QuestionRows = new ObservableCollection<QuestionVerificationRow>(BuildQuestionRows(markingResults));
        }

        private IEnumerable<QuestionVerificationRow> BuildQuestionRows(List<MarkingResult>? markingResults)
        {
            var rows = new List<QuestionVerificationRow>();
            for (int q = 1; q <= OmrConstants.QuestionsCount; q++)
            {
                var markedOptions = markingResults == null
                    ? new List<int>()
                    : markingResults
                        .Where(m => m.QuestionNumber == q && m.IsMarked)
                        .Select(m => m.OptionNumber)
                        .OrderBy(n => n)
                        .ToList();

                if (markingResults == null || markingResults.Count == 0)
                {
                    rows.Add(new QuestionVerificationRow(q, "리딩 결과 없음", null, null, null));
                    continue;
                }

                if (markedOptions.Count == 0)
                {
                    rows.Add(new QuestionVerificationRow(q, "미마킹", null, null, null));
                    continue;
                }

                if (markedOptions.Count > 1)
                {
                    rows.Add(new QuestionVerificationRow(q, $"다중({string.Join(", ", markedOptions)})", null, null, null));
                    continue;
                }

                var option = markedOptions[0];
                var scoreName = GetScoreName(option);
                var scoreValue = _scoringRule?.GetScore(q, option);
                rows.Add(new QuestionVerificationRow(q, option.ToString(), option, scoreName,
                    scoreValue.HasValue ? (double?)scoreValue.Value : null));
            }

            return rows;
        }

        private string? GetScoreName(int optionNumber)
        {
            if (_scoringRule == null) return null;
            var idx = optionNumber - 1;
            if (idx < 0 || idx >= _scoringRule.ScoreNames.Count) return null;
            var name = _scoringRule.ScoreNames[idx];
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        private BitmapSource? RenderOverlayImage(
            ImageDocument document,
            List<MarkingResult>? markingResults,
            List<BarcodeResult>? barcodeResults)
        {
            try
            {
                var imagePath = document.GetImagePathForUse();
                if (!File.Exists(imagePath))
                {
                    Logger.Instance.Warning($"검산 이미지 파일을 찾을 수 없음: {imagePath}");
                    return null;
                }

                var originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.UriSource = new Uri(imagePath, UriKind.Absolute);
                originalImage.EndInit();
                originalImage.Freeze();

                var template = _workspace.Template;

                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                {
                    dc.DrawImage(originalImage, new Rect(0, 0, document.ImageWidth, document.ImageHeight));

                    // TimingMarks (녹색)
                    var timingMarkPen = new Pen(Brushes.Green, 2.0);
                    foreach (var overlay in template.TimingMarks)
                    {
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        dc.DrawRectangle(null, timingMarkPen, rect);
                    }

                    // ScoringAreas (마킹 결과에 따라 색상)
                    var scoringAreas = template.ScoringAreas.ToList();
                    for (int i = 0; i < scoringAreas.Count; i++)
                    {
                        var overlay = scoringAreas[i];
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);

                        Brush? fillBrush = null;
                        Pen? pen = null;

                        if (markingResults != null && i < markingResults.Count)
                        {
                            var result = markingResults[i];
                            if (result.IsMarked)
                            {
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));
                                pen = new Pen(Brushes.Blue, 2.0);
                            }
                            else
                            {
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
                                pen = new Pen(Brushes.Red, 2.0);
                            }
                        }
                        else
                        {
                            pen = new Pen(Brushes.Red, 2.0);
                        }

                        dc.DrawRectangle(fillBrush, pen, rect);
                    }

                    // BarcodeAreas (디코딩 성공/실패)
                    var barcodeAreas = template.BarcodeAreas.ToList();
                    for (int i = 0; i < barcodeAreas.Count; i++)
                    {
                        var overlay = barcodeAreas[i];
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);

                        Brush? fillBrush = null;
                        Pen? pen = null;

                        if (barcodeResults != null && i < barcodeResults.Count)
                        {
                            var result = barcodeResults[i];
                            if (result.Success)
                            {
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 255, 165, 0));
                                pen = new Pen(Brushes.Orange, 2.0);
                            }
                            else
                            {
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 128, 128, 128));
                                pen = new Pen(Brushes.Gray, 2.0);
                            }
                        }
                        else
                        {
                            pen = new Pen(Brushes.Orange, 2.0);
                        }

                        dc.DrawRectangle(fillBrush, pen, rect);
                    }
                }

                var rtb = new RenderTargetBitmap(document.ImageWidth, document.ImageHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);
                rtb.Freeze();

                return rtb;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"검산 오버레이 렌더링 실패: {document.SourcePath}", ex);
                return null;
            }
        }

        private void SelectPreviousImage()
        {
            if (SelectedSheet == null || StudentSheets.Count == 0) return;
            var index = StudentSheets.IndexOf(SelectedSheet);
            if (index <= 0) return;
            SelectedSheet = StudentSheets[index - 1];
        }

        private void SelectNextImage()
        {
            if (SelectedSheet == null || StudentSheets.Count == 0) return;
            var index = StudentSheets.IndexOf(SelectedSheet);
            if (index < 0 || index >= StudentSheets.Count - 1) return;
            SelectedSheet = StudentSheets[index + 1];
        }

        private void RaiseCommandCanExecuteChanged()
        {
            if (RegenerateSampleCommand is RelayCommand regen) regen.RaiseCanExecuteChanged();
            if (PreviousImageCommand is RelayCommand prev) prev.RaiseCanExecuteChanged();
            if (NextImageCommand is RelayCommand next) next.RaiseCanExecuteChanged();
            if (ReloadCommand is RelayCommand reload) reload.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public record StudentSampleItem(string StudentId, int ImageCount, bool HasGrading)
    {
        public override string ToString()
            => $"{StudentId} ({ImageCount}장)" + (HasGrading ? "" : " - 성적없음");
    }

    public record QuestionVerificationRow(
        int QuestionNumber,
        string MarkingStatus,
        int? SelectedOption,
        string? ScoreName,
        double? ScoreValue)
    {
        public string ScoreValueDisplay => ScoreValue.HasValue ? ScoreValue.Value.ToString("0.##") : "";
    }
}
