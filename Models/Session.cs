using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 이미지 로드 및 리딩 작업 세션을 나타냅니다.
    /// 하나의 이미지 로드와 그에 대한 리딩 결과를 하나의 단위로 저장합니다.
    /// </summary>
    public class Session : INotifyPropertyChanged
    {
        private ObservableCollection<ImageDocument> _documents = new();
        private Dictionary<string, List<MarkingResult>> _markingResults = new();
        private Dictionary<string, List<BarcodeResult>> _barcodeResults = new();
        private HashSet<string> _alignmentFailedImageIds = new();
        private HashSet<string> _barcodeFailedImageIds = new();
        private Dictionary<string, IngestDocState> _ingestStateByImageId = new();

        /// <summary>
        /// 이미지 문서 목록 (이미지 로드 시 생성)
        /// </summary>
        public ObservableCollection<ImageDocument> Documents
        {
            get => _documents;
            set
            {
                _documents = value ?? new ObservableCollection<ImageDocument>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 문서별 마킹 리딩 결과 (ImageId -> MarkingResult 리스트)
        /// 리딩 작업 시 생성
        /// </summary>
        public Dictionary<string, List<MarkingResult>> MarkingResults
        {
            get => _markingResults;
            set
            {
                _markingResults = value ?? new Dictionary<string, List<MarkingResult>>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 문서별 바코드 디코딩 결과 (ImageId -> BarcodeResult 리스트)
        /// 폴더 로드 시점에 생성 (정렬 성공한 문서만 대상)
        /// </summary>
        public Dictionary<string, List<BarcodeResult>> BarcodeResults
        {
            get => _barcodeResults;
            set
            {
                _barcodeResults = value ?? new Dictionary<string, List<BarcodeResult>>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 정렬 실패 문서 ID 목록
        /// </summary>
        public HashSet<string> AlignmentFailedImageIds
        {
            get => _alignmentFailedImageIds;
            set
            {
                _alignmentFailedImageIds = value ?? new HashSet<string>();
                OnPropertyChanged();
            }
        }
        /// <summary>
        /// 바코드 디코딩 실패 문서 ID 목록
        /// </summary>
        public HashSet<string> BarcodeFailedImageIds
        {
            get => _barcodeFailedImageIds;
            set
            {
                _barcodeFailedImageIds = value ?? new HashSet<string>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// ingest 상태 추적 정보 (ImageId -> IngestDocState)
        /// </summary>
        public Dictionary<string, IngestDocState> IngestStateByImageId
        {
            get => _ingestStateByImageId;
            set
            {
                _ingestStateByImageId = value ?? new Dictionary<string, IngestDocState>();
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
