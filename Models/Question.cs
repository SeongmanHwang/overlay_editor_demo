using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 하나의 문항을 나타냅니다. 12개의 선택지(옵션)를 포함합니다.
    /// </summary>
    public class Question : INotifyPropertyChanged
    {
        private int _questionNumber;
        private ObservableCollection<RectangleOverlay> _options = new();

        /// <summary>
        /// 문항 번호 (1-4)
        /// </summary>
        public int QuestionNumber
        {
            get => _questionNumber;
            set
            {
                _questionNumber = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 이 문항의 선택지 목록 (12개)
        /// </summary>
        public ObservableCollection<RectangleOverlay> Options
        {
            get => _options;
            set
            {
                _options = value ?? new ObservableCollection<RectangleOverlay>();
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
