using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 이미지 정렬 정보를 저장합니다.
    /// </summary>
    public class AlignmentInfo : INotifyPropertyChanged
    {
        private bool _success;
        private double _confidence;
        private double _rotation;
        private double _scaleX;
        private double _scaleY;
        private double _translationX;
        private double _translationY;
        private string? _alignedImagePath;

        /// <summary>
        /// 정렬 성공 여부
        /// </summary>
        public bool Success
        {
            get => _success;
            set { _success = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 정렬 신뢰도 (0.0 ~ 1.0)
        /// </summary>
        public double Confidence
        {
            get => _confidence;
            set { _confidence = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 회전 각도 (도 단위)
        /// </summary>
        public double Rotation
        {
            get => _rotation;
            set { _rotation = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// X축 스케일
        /// </summary>
        public double ScaleX
        {
            get => _scaleX;
            set { _scaleX = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Y축 스케일
        /// </summary>
        public double ScaleY
        {
            get => _scaleY;
            set { _scaleY = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// X축 이동 거리 (픽셀)
        /// </summary>
        public double TranslationX
        {
            get => _translationX;
            set { _translationX = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Y축 이동 거리 (픽셀)
        /// </summary>
        public double TranslationY
        {
            get => _translationY;
            set { _translationY = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 정렬된 이미지 캐시 경로 (정렬 성공 시에만 설정)
        /// </summary>
        public string? AlignedImagePath
        {
            get => _alignedImagePath;
            set { _alignedImagePath = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

