using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 정답 및 배점 정보를 나타냅니다.
    /// 4문항 × 12선택지의 배점 정보를 저장합니다.
    /// </summary>
    public class ScoringRule : INotifyPropertyChanged
    {
        private ObservableCollection<QuestionScoringRule> _questions;
        private ObservableCollection<string> _scoreNames;

        public ScoringRule()
        {
            _questions = new ObservableCollection<QuestionScoringRule>();
            _scoreNames = new ObservableCollection<string>();
            
            // 4개 문항 초기화
            for (int i = 1; i <= 4; i++)
            {
                _questions.Add(new QuestionScoringRule { QuestionNumber = i });
            }
            
            // 12개 선택지에 대한 점수 이름 초기화 (기본값: 빈 문자열)
            for (int i = 0; i < 12; i++)
            {
                _scoreNames.Add(string.Empty);
            }
        }

        /// <summary>
        /// 문항별 배점 정보 (4개 문항)
        /// </summary>
        public ObservableCollection<QuestionScoringRule> Questions
        {
            get => _questions;
            set
            {
                _questions = value ?? new ObservableCollection<QuestionScoringRule>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 선택지별 점수 이름 (인덱스 0~11 = 1번~12번 선택지)
        /// 예: A, B, C, D, ...
        /// </summary>
        public ObservableCollection<string> ScoreNames
        {
            get => _scoreNames;
            set
            {
                _scoreNames = value ?? new ObservableCollection<string>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 특정 문항, 특정 선택지의 배점을 가져옵니다.
        /// </summary>
        public double GetScore(int questionNumber, int optionNumber)
        {
            var question = Questions.FirstOrDefault(q => q.QuestionNumber == questionNumber);
            if (question == null || optionNumber < 1 || optionNumber > 12)
                return 0;

            return question.GetScore(optionNumber);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 한 문항의 배점 정보 (12개 선택지)
    /// </summary>
    public class QuestionScoringRule : INotifyPropertyChanged
    {
        private int _questionNumber;
        private ObservableCollection<double> _scores;

        public QuestionScoringRule()
        {
            _scores = new ObservableCollection<double>();
            
            // 12개 선택지 초기화 (모두 0점)
            for (int i = 0; i < 12; i++)
            {
                _scores.Add(0);
            }
        }

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
        /// 선택지별 배점 (인덱스 0~11 = 1번~12번 선택지)
        /// </summary>
        public ObservableCollection<double> Scores
        {
            get => _scores;
            set
            {
                _scores = value ?? new ObservableCollection<double>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 특정 선택지 번호(1~12)의 배점을 가져옵니다.
        /// </summary>
        public double GetScore(int optionNumber)
        {
            if (optionNumber < 1 || optionNumber > 12)
                return 0;
            
            int index = optionNumber - 1;
            if (index < Scores.Count)
                return Scores[index];
            
            return 0;
        }

        /// <summary>
        /// 특정 선택지 번호(1~12)의 배점을 설정합니다.
        /// </summary>
        public void SetScore(int optionNumber, double score)
        {
            if (optionNumber < 1 || optionNumber > 12)
                return;
            
            int index = optionNumber - 1;
            
            // 인덱스가 범위를 벗어나면 확장
            while (Scores.Count <= index)
            {
                Scores.Add(0);
            }
            
            Scores[index] = score;
            OnPropertyChanged(nameof(Scores));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
