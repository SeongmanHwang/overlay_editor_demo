using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// OMR 템플릿을 관리합니다. 모든 이미지에 공통으로 적용되는 타이밍 마크와 채점 영역을 포함합니다.
    /// </summary>
    public class OmrTemplate : INotifyPropertyChanged
    {
        private ObservableCollection<RectangleOverlay> _timingMarks = new();
        private ObservableCollection<RectangleOverlay> _scoringAreas = new();
        private ObservableCollection<RectangleOverlay> _barcodeAreas = new();
        private int _referenceWidth;
        private int _referenceHeight;

        /// <summary>
        /// 타이밍 마크 오버레이 목록 (상단에 위치, 이미지 정렬용)
        /// </summary>
        public ObservableCollection<RectangleOverlay> TimingMarks
        {
            get => _timingMarks;
            set
            {
                _timingMarks = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 채점 영역 오버레이 목록 (우측에 위치, 마킹 감지용)
        /// </summary>
        public ObservableCollection<RectangleOverlay> ScoringAreas
        {
            get => _scoringAreas;
            set
            {
                _scoringAreas = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 바코드 영역 오버레이 목록 (좌측에 위치, 수험번호/면접위원 번호 바코드 디코딩용)
        /// </summary>
        public ObservableCollection<RectangleOverlay> BarcodeAreas
        {
            get => _barcodeAreas;
            set
            {
                _barcodeAreas = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 템플릿의 기준 이미지 너비 (픽셀)
        /// </summary>
        public int ReferenceWidth
        {
            get => _referenceWidth;
            set
            {
                _referenceWidth = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 템플릿의 기준 이미지 높이 (픽셀)
        /// </summary>
        public int ReferenceHeight
        {
            get => _referenceHeight;
            set
            {
                _referenceHeight = value;
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

