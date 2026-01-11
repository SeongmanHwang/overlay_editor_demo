using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 면접위원 정보
    /// </summary>
    public class InterviewerInfo : INotifyPropertyChanged
    {
        private string _interviewerId = string.Empty;
        private string? _name;

        public string InterviewerId
        {
            get => _interviewerId;
            set { _interviewerId = value; OnPropertyChanged(); }
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
    /// 면접위원 명부
    /// </summary>
    public class InterviewerRegistry : INotifyPropertyChanged
    {
        private ObservableCollection<InterviewerInfo> _interviewers = new();

        public ObservableCollection<InterviewerInfo> Interviewers
        {
            get => _interviewers;
            set
            {
                _interviewers = value ?? new ObservableCollection<InterviewerInfo>();
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
