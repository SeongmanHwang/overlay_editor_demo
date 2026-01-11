using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 수험생 정보 (첫 열: 수험번호만 활용, 나머지는 참고용)
    /// </summary>
    public class StudentInfo : INotifyPropertyChanged
    {
        private string _studentId = string.Empty;
        private string? _time;
        private string? _group;
        private string? _interviewRoom;
        private string? _number;
        private string? _registrationNumber;
        private string? _middleSchool;
        private string? _name;

        public string StudentId
        {
            get => _studentId;
            set { _studentId = value; OnPropertyChanged(); }
        }

        public string? Time
        {
            get => _time;
            set { _time = value; OnPropertyChanged(); }
        }

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

        public string? Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 수험생 명부
    /// </summary>
    public class StudentRegistry : INotifyPropertyChanged
    {
        private ObservableCollection<StudentInfo> _students = new();

        public ObservableCollection<StudentInfo> Students
        {
            get => _students;
            set
            {
                _students = value ?? new ObservableCollection<StudentInfo>();
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
