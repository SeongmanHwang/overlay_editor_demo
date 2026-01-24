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
    /// 수기검산/단일학생검산에서 공통으로 사용하는 OMR 검산 코어(데이터 로드, 학생 선택, 이미지/표 렌더).
    /// </summary>
    public sealed class OmrVerificationCore : INotifyPropertyChanged
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

        private bool _isBusy;
        private string? _busyMessage;
        private string? _currentStudentId;
        private ObservableCollection<OmrSheetResult> _studentSheets = new();
        private OmrSheetResult? _selectedSheet;
        private ImageDocument? _selectedDocument;
        private BitmapSource? _displayImage;
        private ObservableCollection<QuestionVerificationRow> _questionRows = new();
        private GradingResult? _selectedStudentGradingResult;
        private Rect _currentImageDisplayRect;
        private double _zoomLevel = 1.0;

        public OmrVerificationCore(NavigationViewModel navigation, Workspace workspace)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));

            _sessionStore = new SessionStore();
            _scoringRuleStore = new ScoringRuleStore();
            _markingAnalyzer = new MarkingAnalyzer();

            PreviousImageCommand = new RelayCommand(SelectPreviousImage, () => !IsBusy && StudentSheets.Count > 0 && SelectedSheet != null);
            NextImageCommand = new RelayCommand(SelectNextImage, () => !IsBusy && StudentSheets.Count > 0 && SelectedSheet != null);
        }

        public NavigationViewModel Navigation => _navigation;
        public Workspace Workspace => _workspace;

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

        public string? CurrentStudentId
        {
            get => _currentStudentId;
            private set
            {
                if (_currentStudentId != value)
                {
                    _currentStudentId = value;
                    OnPropertyChanged();
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

        public ICommand PreviousImageCommand { get; }
        public ICommand NextImageCommand { get; }

        public IReadOnlyList<OmrSheetResult> AllSheetResults => _allSheetResults;

        public async Task EnsureLoadedAsync()
        {
            if (_session != null && _scoringRule != null && _allSheetResults.Count > 0) return;
            await ReloadAsync();
        }

        public async Task ReloadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            BusyMessage = "검산 데이터 로드 중...";

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
                });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("검산 코어 로드 실패", ex);
                UiThread.Invoke(() =>
                {
                    _session = null;
                    _scoringRule = null;
                    _allSheetResults = new List<OmrSheetResult>();
                    _documentByImageId = new Dictionary<string, ImageDocument>();
                    _gradingByStudentId = new Dictionary<string, GradingResult>();
                    CurrentStudentId = null;
                    StudentSheets = new ObservableCollection<OmrSheetResult>();
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

            // 성적처리 결과(표) 조회: 기존과 동일하게 GradingViewModel을 headless로 생성
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

        public bool SetStudent(string studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                ClearStudent();
                return false;
            }

            var id = studentId.Trim();
            var sheets = _allSheetResults
                .Where(r => r.StudentId == id)
                .OrderBy(r => TryParseIntOrMax(r.InterviewId))
                .ThenBy(r => r.InterviewId ?? "", StringComparer.Ordinal)
                .ThenBy(r => r.ImageFileName ?? "", StringComparer.Ordinal)
                .ToList();

            if (sheets.Count == 0)
            {
                ClearStudent();
                CurrentStudentId = id;
                // 안내성으로 비어있음을 표시 (이미지는 없음)
                return false;
            }

            CurrentStudentId = id;
            StudentSheets = new ObservableCollection<OmrSheetResult>(sheets);
            SelectedSheet = StudentSheets.FirstOrDefault();
            SelectedStudentGradingResult = _gradingByStudentId.TryGetValue(id, out var result) ? result : null;
            return true;
        }

        public void ClearStudent()
        {
            CurrentStudentId = null;
            StudentSheets = new ObservableCollection<OmrSheetResult>();
            SelectedSheet = null;
            SelectedStudentGradingResult = null;
            SelectedDocument = null;
            DisplayImage = null;
            QuestionRows = new ObservableCollection<QuestionVerificationRow>();
        }

        public void ReleaseHeavyResources()
        {
            DisplayImage = null;
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

        private void UpdateSelectedSheetDerivedState()
        {
            if (SelectedSheet == null || _session == null)
            {
                SelectedDocument = null;
                DisplayImage = null;
                QuestionRows = new ObservableCollection<QuestionVerificationRow>();
                return;
            }

            if (!string.IsNullOrEmpty(SelectedSheet.StudentId))
            {
                CurrentStudentId = SelectedSheet.StudentId;
                SelectedStudentGradingResult = _gradingByStudentId.TryGetValue(SelectedSheet.StudentId, out var result) ? result : null;
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

            DisplayImage = (SelectedDocument != null)
                ? RenderOverlayImage(SelectedDocument, markingResults, barcodeResults)
                : null;

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

                    var timingMarkPen = new Pen(Brushes.Green, 2.0);
                    foreach (var overlay in template.TimingMarks)
                    {
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        dc.DrawRectangle(null, timingMarkPen, rect);
                    }

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
            if (PreviousImageCommand is RelayCommand prev) prev.RaiseCanExecuteChanged();
            if (NextImageCommand is RelayCommand next) next.RaiseCanExecuteChanged();
        }

        private static int TryParseIntOrMax(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return int.MaxValue;
            return int.TryParse(s.Trim(), out var n) ? n : int.MaxValue;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

