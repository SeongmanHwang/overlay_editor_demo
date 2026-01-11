using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 하나의 OMR 시트의 전체 결과를 나타냅니다.
    /// </summary>
    public class OmrSheetResult : INotifyPropertyChanged
    {
        private string _imageId = string.Empty;
        private string _imageFileName = string.Empty;
        private string? _studentId; // 수험번호 (바코드 1)
        private string? _interviewId; // 면접번호 (바코드 2)
        private int? _question1Marking; // 문항1 마킹 (1-12, null = 미마킹 또는 다중마킹)
        private int? _question2Marking;
        private int? _question3Marking;
        private int? _question4Marking;
        private bool _hasErrors; // 오류 여부 (바코드 실패, 다중마킹 등)
        private string? _errorMessage;
        private bool _isDuplicate; // 결합ID 기준 중복 여부

        public string ImageId
        {
            get => _imageId;
            set { _imageId = value; OnPropertyChanged(); }
        }

        public string ImageFileName
        {
            get => _imageFileName;
            set { _imageFileName = value; OnPropertyChanged(); }
        }

        public string? StudentId
        {
            get => _studentId;
            set { _studentId = value; OnPropertyChanged(); OnPropertyChanged(nameof(CombinedId)); }
        }

        public string? InterviewId
        {
            get => _interviewId;
            set { _interviewId = value; OnPropertyChanged(); OnPropertyChanged(nameof(CombinedId)); }
        }

        /// <summary>
        /// 수험번호와 면접번호를 결합한 ID (하이픈/언더스코어 없이 연결)
        /// </summary>
        public string? CombinedId
        {
            get
            {
                if (string.IsNullOrEmpty(_studentId) && string.IsNullOrEmpty(_interviewId))
                    return null;
                
                if (string.IsNullOrEmpty(_studentId))
                    return _interviewId;
                
                if (string.IsNullOrEmpty(_interviewId))
                    return _studentId;
                
                return $"{_studentId}{_interviewId}";
            }
        }

        public int? Question1Marking
        {
            get => _question1Marking;
            set { _question1Marking = value; OnPropertyChanged(); }
        }

        public int? Question2Marking
        {
            get => _question2Marking;
            set { _question2Marking = value; OnPropertyChanged(); }
        }

        public int? Question3Marking
        {
            get => _question3Marking;
            set { _question3Marking = value; OnPropertyChanged(); }
        }

        public int? Question4Marking
        {
            get => _question4Marking;
            set { _question4Marking = value; OnPropertyChanged(); }
        }

        public bool HasErrors
        {
            get => _hasErrors;
            set { _hasErrors = value; OnPropertyChanged(); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 결합ID 기준으로 중복된 데이터인지 여부
        /// </summary>
        public bool IsDuplicate
        {
            get => _isDuplicate;
            set { _isDuplicate = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
