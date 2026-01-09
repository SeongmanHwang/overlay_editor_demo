using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 바코드 디코딩 결과를 저장합니다.
    /// </summary>
    public class BarcodeResult : INotifyPropertyChanged
    {
        private string _barcodeAreaId = string.Empty;
        private string? _decodedText;
        private bool _success;
        private string? _format; // 바코드 포맷 (예: "CODE_128", "CODE_39" 등)
        private string? _errorMessage;

        /// <summary>
        /// 바코드 영역의 고유 식별자 (인덱스 또는 ID)
        /// </summary>
        public string BarcodeAreaId
        {
            get => _barcodeAreaId;
            set { _barcodeAreaId = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 디코딩된 바코드 텍스트
        /// </summary>
        public string? DecodedText
        {
            get => _decodedText;
            set { _decodedText = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 바코드 디코딩 성공 여부
        /// </summary>
        public bool Success
        {
            get => _success;
            set { _success = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 바코드 포맷 (예: "CODE_128", "CODE_39", "EAN_13" 등)
        /// </summary>
        public string? Format
        {
            get => _format;
            set { _format = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 디코딩 실패 시 오류 메시지
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

