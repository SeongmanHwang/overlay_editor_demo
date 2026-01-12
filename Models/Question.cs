using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 하나의 문항을 나타냅니다. 12개의 선택지(옵션) 슬롯을 고정으로 포함합니다.
    /// </summary>
    public class Question : INotifyPropertyChanged
    {
        private int _questionNumber;
        private ObservableCollection<RectangleOverlay> _options = new();

        public Question()
        {
            // 12개 슬롯을 미리 생성 (빈 상태)
            for (int i = 1; i <= OmrConstants.OptionsPerQuestion; i++)
            {
                var slot = new RectangleOverlay
                {
                    OptionNumber = i,  // IdentityIndex 설정
                    QuestionNumber = null, // QuestionNumber 설정 시 업데이트됨
                    Width = 0,
                    Height = 0,
                    OverlayType = OverlayType.ScoringArea
                };
                _options.Add(slot);
            }
        }

        /// <summary>
        /// 문항 번호 (1-{OmrConstants.QuestionsCount})
        /// </summary>
        public int QuestionNumber
        {
            get => _questionNumber;
            set
            {
                _questionNumber = value;
                // 각 슬롯의 QuestionNumber도 업데이트
                foreach (var option in _options)
                {
                    option.QuestionNumber = value;
                }
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 이 문항의 선택지 목록 ({OmrConstants.OptionsPerQuestion}개 슬롯 고정)
        /// </summary>
        public ObservableCollection<RectangleOverlay> Options
        {
            get => _options;
            set
            {
                // 슬롯 구조에서는 Options를 완전히 교체하지 않도록 주의
                // 필요시 마이그레이션 로직 추가 가능
                _options = value ?? new ObservableCollection<RectangleOverlay>();
                // QuestionNumber 업데이트
                foreach (var option in _options)
                {
                    option.QuestionNumber = _questionNumber;
                }
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
