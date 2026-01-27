using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.ViewModels
{
    public partial class MarkingViewModel
    {
        private readonly HashSet<string> _currentLoadImageIds = new();
        private ObservableCollection<LoadFailureItem> _loadFailureItems = new();
        private bool _isLoadFailurePanelExpanded;

        public ObservableCollection<LoadFailureItem> LoadFailureItems
        {
            get => _loadFailureItems;
            private set
            {
                _loadFailureItems = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LoadFailureCount));
                OnPropertyChanged(nameof(AlignFailureCount));
                OnPropertyChanged(nameof(BarcodeFailureCount));
                OnPropertyChanged(nameof(CombinedIdMissingCount));
                OnPropertyChanged(nameof(MissingFileCount));
                OnPropertyChanged(nameof(HasLoadFailureItems));
            }
        }

        public int LoadFailureCount => LoadFailureItems?.Count ?? 0;
        public int AlignFailureCount => LoadFailureItems?.Count(item => item.FailureReasons.HasFlag(IngestFailureReason.AlignFailed)) ?? 0;
        public int BarcodeFailureCount => LoadFailureItems?.Count(item => item.FailureReasons.HasFlag(IngestFailureReason.BarcodeFailed)) ?? 0;
        public int CombinedIdMissingCount => LoadFailureItems?.Count(item => item.FailureReasons.HasFlag(IngestFailureReason.CombinedIdMissing)) ?? 0;
        public int MissingFileCount => LoadFailureItems?.Count(item => item.FailureReasons.HasFlag(IngestFailureReason.MissingFile)) ?? 0;
        public bool HasLoadFailureItems => LoadFailureCount > 0;

        public bool IsLoadFailurePanelExpanded
        {
            get => _isLoadFailurePanelExpanded;
            set
            {
                if (_isLoadFailurePanelExpanded != value)
                {
                    _isLoadFailurePanelExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateLoadFailureItems(IEnumerable<ImageDocument>? currentLoadDocuments)
        {
            if (currentLoadDocuments != null)
            {
                _currentLoadImageIds.Clear();
                foreach (var doc in currentLoadDocuments)
                {
                    _currentLoadImageIds.Add(doc.ImageId);
                }
            }

            if (_currentLoadImageIds.Count == 0)
            {
                LoadFailureItems.Clear();
                IsLoadFailurePanelExpanded = false;
                OnPropertyChanged(nameof(LoadFailureCount));
                OnPropertyChanged(nameof(AlignFailureCount));
                OnPropertyChanged(nameof(BarcodeFailureCount));
                OnPropertyChanged(nameof(CombinedIdMissingCount));
                OnPropertyChanged(nameof(MissingFileCount));
                OnPropertyChanged(nameof(HasLoadFailureItems));
                return;
            }

            var previousCount = LoadFailureItems.Count;
            var updated = new List<LoadFailureItem>();

            foreach (var imageId in _currentLoadImageIds)
            {
                if (!_session.IngestStateByImageId.TryGetValue(imageId, out var state) || state == null)
                {
                    continue;
                }

                if (!state.IsQuarantined)
                {
                    continue;
                }

                var fileName = ResolveFileName(imageId);
                var summary = FormatFailureReasons(state.FailureReasons);
                updated.Add(new LoadFailureItem(imageId, fileName, state.FailureReasons, summary));
            }

            LoadFailureItems.Clear();
            foreach (var item in updated)
            {
                LoadFailureItems.Add(item);
            }

            if (previousCount == 0 && LoadFailureItems.Count > 0)
            {
                IsLoadFailurePanelExpanded = true;
            }

            OnPropertyChanged(nameof(LoadFailureCount));
            OnPropertyChanged(nameof(AlignFailureCount));
            OnPropertyChanged(nameof(BarcodeFailureCount));
            OnPropertyChanged(nameof(CombinedIdMissingCount));
            OnPropertyChanged(nameof(MissingFileCount));
            OnPropertyChanged(nameof(HasLoadFailureItems));
        }

        private string ResolveFileName(string imageId)
        {
            var document = Documents?.FirstOrDefault(doc => doc.ImageId == imageId);
            if (document == null || string.IsNullOrWhiteSpace(document.SourcePath))
            {
                return imageId;
            }

            return Path.GetFileName(document.SourcePath) ?? imageId;
        }

        private static string FormatFailureReasons(IngestFailureReason reasons)
        {
            if (reasons == IngestFailureReason.None)
            {
                return "사유 미기록";
            }

            var labels = new List<string>();
            if (reasons.HasFlag(IngestFailureReason.AlignFailed))
            {
                labels.Add("정렬 실패");
            }

            if (reasons.HasFlag(IngestFailureReason.BarcodeFailed))
            {
                labels.Add("바코드 실패");
            }

            if (reasons.HasFlag(IngestFailureReason.CombinedIdMissing))
            {
                labels.Add("ID 없음");
            }

            if (reasons.HasFlag(IngestFailureReason.MissingFile))
            {
                labels.Add("파일 누락");
            }

            return string.Join(" / ", labels);
        }
    }
}