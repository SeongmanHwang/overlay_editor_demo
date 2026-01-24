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
        private double? _question1Marking;
        private double? _question2Marking;
        private double? _question3Marking;
        private double? _question4Marking;
        private double? _totalScore;
        private double? _averageScore;
        private int? _totalScoreRaw;
        private int? _rank;
        private bool _isDuplicate;  // "수험번호 + 면접번호" 결합 ID 기준 중복 여부
        private int _duplicateCount;  // 같은 "수험번호 + 면접번호" 조합을 가진 시트 수
        private bool _isSimpleError;  // 단순 오류 여부 (마킹, 바코드 등, 중복 제외)
        private string? _errorDetails; // 오류 상세 정보
        
        // StudentInfo의 추가 정보들
        private string? _registrationNumber;
        private string? _examType;  // 전형명
        private string? _middleSchool;  // 출신교명
        private string? _birthDate;  // 생년월일

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
            set 
            { 
                _studentId = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(Session));
                OnPropertyChanged(nameof(RoomNumber));
                OnPropertyChanged(nameof(OrderNumber));
            }
        }

        /// <summary>
        /// 오전/오후 세션 (수험번호의 1-2번째 자리: 91=오전, 92=오후)
        /// </summary>
        public string? Session
        {
            get
            {
                if (string.IsNullOrEmpty(_studentId) || _studentId.Length < 2)
                    return null;
                
                var sessionCode = _studentId.Substring(0, 2);
                return sessionCode switch
                {
                    "91" => "오전",
                    "92" => "오후",
                    _ => null
                };
            }
        }

        /// <summary>
        /// 면접실 번호 (수험번호의 3-4번째 자리)
        /// </summary>
        public string? RoomNumber
        {
            get
            {
                if (string.IsNullOrEmpty(_studentId) || _studentId.Length < 4)
                    return null;
                
                var roomCode = _studentId.Substring(2, 2);
                if (int.TryParse(roomCode, out var roomNum) && roomNum >= 1 && roomNum <= 12)
                    return roomCode;
                
                return null;
            }
        }

        /// <summary>
        /// 순서 번호 (수험번호의 5-6번째 자리)
        /// </summary>
        public string? OrderNumber
        {
            get
            {
                if (string.IsNullOrEmpty(_studentId) || _studentId.Length < 6)
                    return null;
                
                return _studentId.Substring(4, 2);
            }
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

        public double? Question1Marking
        {
            get => _question1Marking;
            set { _question1Marking = value; OnPropertyChanged(); }
        }

        public double? Question2Marking
        {
            get => _question2Marking;
            set { _question2Marking = value; OnPropertyChanged(); }
        }

        public double? Question3Marking
        {
            get => _question3Marking;
            set { _question3Marking = value; OnPropertyChanged(); }
        }

        public double? Question4Marking
        {
            get => _question4Marking;
            set { _question4Marking = value; OnPropertyChanged(); }
        }

        public double? TotalScore
        {
            get => _totalScore;
            set { _totalScore = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 석차 계산에 사용하는 총점(정수 합계, 분자). 평균/반올림/부동소수점과 무관하게 안정적입니다.
        /// </summary>
        public int? TotalScoreRaw
        {
            get => _totalScoreRaw;
            set { _totalScoreRaw = value; OnPropertyChanged(); }
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
            set 
            { 
                _isDuplicate = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        /// <summary>
        /// 같은 "수험번호 + 면접번호" 조합을 가진 시트 수
        /// </summary>
        public int DuplicateCount
        {
            get => _duplicateCount;
            set { _duplicateCount = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 단순 오류 여부 (마킹, 바코드 등, 중복 제외)
        /// </summary>
        public bool IsSimpleError
        {
            get => _isSimpleError;
            set 
            { 
                _isSimpleError = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(HasErrors));
            }
        }

        /// <summary>
        /// 모든 오류 여부 (단순 오류 또는 중복 오류) - 계산 속성
        /// </summary>
        public bool HasErrors => IsSimpleError || IsDuplicate;

        /// <summary>
        /// 오류 상세 정보 (수험번호별 오류 요약에 사용)
        /// </summary>
        public string? ErrorDetails
        {
            get => _errorDetails;
            set { _errorDetails = value; OnPropertyChanged(); }
        }

        // StudentInfo 필드들
        public string? RegistrationNumber
        {
            get => _registrationNumber;
            set { _registrationNumber = value; OnPropertyChanged(); }
        }

        public string? ExamType
        {
            get => _examType;
            set { _examType = value; OnPropertyChanged(); }
        }

        public string? MiddleSchool
        {
            get => _middleSchool;
            set { _middleSchool = value; OnPropertyChanged(); }
        }

        public string? BirthDate
        {
            get => _birthDate;
            set { _birthDate = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
