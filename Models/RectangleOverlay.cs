using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    public class RectangleOverlay : INotifyPropertyChanged
    {
        private double _x;
        private double _y;
        private double _width;
        private double _height;
        private double _strokeThickness = 2.0;
        private OverlayType _overlayType = OverlayType.ScoringArea;
        private int? _optionNumber; // 선택지 번호 (ScoringArea일 때만 사용, 1-12)
        private int? _questionNumber; // 문항 번호 (ScoringArea일 때만 사용, 1-4)

        public double X
        {
            get => _x;
            set { _x = value; OnPropertyChanged(); }
        }

        public double Y
        {
            get => _y;
            set { _y = value; OnPropertyChanged(); }
        }

        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPlaced)); }
        }

        public double Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPlaced)); }
        }

        public double StrokeThickness
        {
            get => _strokeThickness;
            set { _strokeThickness = value; OnPropertyChanged(); }
        }

        public OverlayType OverlayType
        {
            get => _overlayType;
            set { _overlayType = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 선택지 번호 (ScoringArea일 때만 사용, 1-12)
        /// IdentityIndex: 마킹 리딩과 정확히 연결되는 고정 번호
        /// null이면 아직 배치되지 않은 슬롯이거나 ScoringArea가 아닌 경우
        /// </summary>
        public int? OptionNumber
        {
            get => _optionNumber;
            set
            {
                _optionNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPlaced));
            }
        }

        /// <summary>
        /// 문항 번호 (ScoringArea일 때만 사용, 1-4)
        /// </summary>
        public int? QuestionNumber
        {
            get => _questionNumber;
            set
            {
                _questionNumber = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 이 오버레이가 실제로 배치되었는지 여부
        /// (OptionNumber가 설정되고 좌표가 유효한 경우)
        /// </summary>
        public bool IsPlaced => OptionNumber.HasValue && Width > 0 && Height > 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


