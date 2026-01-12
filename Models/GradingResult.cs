using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 채점 및 성적 처리 결과를 나타냅니다.
    /// OmrSheetResult와 StudentInfo를 조인한 결과입니다.
    /// </summary>
    public class GradingResult : INotifyPropertyChanged
    {
        private string _imageId = string.Empty;
        private string _imageFileName = string.Empty;
        private string? _studentId;
        private string? _studentName;  // StudentInfo에서 lookup
        private string? _interviewId;
        private int? _question1Marking;
        private int? _question2Marking;
        private int? _question3Marking;
        private int? _question4Marking;
        private double? _totalScore;
        private double? _averageScore;
        private int? _rank;
        private bool _isDuplicate;  // "수험번호 + 면접번호" 결합 ID 기준 중복 여부
        private int _duplicateCount;  // 같은 "수험번호 + 면접번호" 조합을 가진 시트 수
        private bool _hasErrors;  // OmrSheetResult의 오류 정보
        
        // StudentInfo의 추가 정보들
        private string? _group;
        private string? _interviewRoom;
        private string? _time;
        private string? _number;
        private string? _registrationNumber;
        private string? _middleSchool;

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
            set { _studentId = value; OnPropertyChanged(); }
        }

        public string? StudentName
        {
            get => _studentName;
            set { _studentName = value; OnPropertyChanged(); }
        }

        public string? InterviewId
        {
            get => _interviewId;
            set { _interviewId = value; OnPropertyChanged(); }
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

        public double? TotalScore
        {
            get => _totalScore;
            set { _totalScore = value; OnPropertyChanged(); }
        }

        public double? AverageScore
        {
            get => _averageScore;
            set { _averageScore = value; OnPropertyChanged(); }
        }

        public int? Rank
        {
            get => _rank;
            set { _rank = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// "수험번호 + 면접번호" 결합 ID 기준 중복 여부 (같은 조합으로 여러 시트가 있는 경우)
        /// </summary>
        public bool IsDuplicate
        {
            get => _isDuplicate;
            set { _isDuplicate = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 같은 "수험번호 + 면접번호" 조합을 가진 시트 수
        /// </summary>
        public int DuplicateCount
        {
            get => _duplicateCount;
            set { _duplicateCount = value; OnPropertyChanged(); }
        }

        public bool HasErrors
        {
            get => _hasErrors;
            set { _hasErrors = value; OnPropertyChanged(); }
        }

        // StudentInfo 필드들
        public string? Group
        {
            get => _group;
            set { _group = value; OnPropertyChanged(); }
        }

        public string? InterviewRoom
        {
            get => _interviewRoom;
            set { _interviewRoom = value; OnPropertyChanged(); }
        }

        public string? Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged(); }
        }

        public string? Number
        {
            get => _number;
            set { _number = value; OnPropertyChanged(); }
        }

        public string? RegistrationNumber
        {
            get => _registrationNumber;
            set { _registrationNumber = value; OnPropertyChanged(); }
        }

        public string? MiddleSchool
        {
            get => _middleSchool;
            set { _middleSchool = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
