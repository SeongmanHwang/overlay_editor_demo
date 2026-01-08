using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 마킹 감지 결과를 저장합니다.
    /// </summary>
    public class MarkingResult : INotifyPropertyChanged
    {
        private string _scoringAreaId = string.Empty;
        private bool _isMarked;
        private double _averageBrightness;
        private double _threshold = 128.0; // 기본 임계값

        /// <summary>
        /// 채점 영역의 고유 식별자 (인덱스 또는 ID)
        /// </summary>
        public string ScoringAreaId
        {
            get => _scoringAreaId;
            set { _scoringAreaId = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 마킹이 감지되었는지 여부
        /// </summary>
        public bool IsMarked
        {
            get => _isMarked;
            set { _isMarked = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// ROI 영역의 평균 밝기 (0-255)
        /// </summary>
        public double AverageBrightness
        {
            get => _averageBrightness;
            set { _averageBrightness = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 마킹 감지에 사용된 임계값
        /// </summary>
        public double Threshold
        {
            get => _threshold;
            set { _threshold = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 이미지 문서별 마킹 감지 결과를 저장합니다.
    /// </summary>
    public class DocumentMarkingResults : INotifyPropertyChanged
    {
        private string _documentId = string.Empty;
        private List<MarkingResult> _results = new();

        public string DocumentId
        {
            get => _documentId;
            set { _documentId = value; OnPropertyChanged(); }
        }

        public List<MarkingResult> Results
        {
            get => _results;
            set { _results = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

