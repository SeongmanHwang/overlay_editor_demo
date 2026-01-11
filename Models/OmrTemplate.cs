using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// OMR 템플릿을 관리합니다. 모든 이미지에 공통으로 적용되는 타이밍 마크와 채점 영역을 포함합니다.
    /// </summary>
    public class OmrTemplate : INotifyPropertyChanged
    {
        private ObservableCollection<RectangleOverlay> _timingMarks = new();
        private ObservableCollection<RectangleOverlay> _scoringAreas = new();
        private ObservableCollection<RectangleOverlay> _barcodeAreas = new();
        private ObservableCollection<Question> _questions = new();
        private int _referenceWidth;
        private int _referenceHeight;

        public OmrTemplate()
        {
            // Questions 초기화 (4개 문항)
            for (int i = 1; i <= 4; i++)
            {
                var question = new Question { QuestionNumber = i };
                question.Options.CollectionChanged += (s, e) => SyncQuestionsToScoringAreas();
                _questions.Add(question);
            }

            // Questions 변경 시 ScoringAreas 자동 동기화
            _questions.CollectionChanged += (s, e) =>
            {
                // 새로 추가된 Question에 이벤트 구독
                if (e.NewItems != null)
                {
                    foreach (Question question in e.NewItems)
                    {
                        question.Options.CollectionChanged += (q, args) => SyncQuestionsToScoringAreas();
                    }
                }
                SyncQuestionsToScoringAreas();
            };
        }

        /// <summary>
        /// 타이밍 마크 오버레이 목록 (상단에 위치, 이미지 정렬용)
        /// </summary>
        public ObservableCollection<RectangleOverlay> TimingMarks
        {
            get => _timingMarks;
            set
            {
                _timingMarks = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 채점 영역 오버레이 목록 (우측에 위치, 마킹 감지용)
        /// Questions에서 자동으로 동기화됩니다. 직접 수정하지 마세요.
        /// </summary>
        public ObservableCollection<RectangleOverlay> ScoringAreas
        {
            get => _scoringAreas;
            private set
            {
                _scoringAreas = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 문항 목록 (4개 문항, 각 12개 선택지)
        /// </summary>
        public ObservableCollection<Question> Questions
        {
            get => _questions;
            set
            {
                if (_questions != null)
                {
                    // 기존 이벤트 구독 해제
                    _questions.CollectionChanged -= (s, e) => SyncQuestionsToScoringAreas();
                    foreach (var question in _questions)
                    {
                        question.Options.CollectionChanged -= (s, e) => SyncQuestionsToScoringAreas();
                    }
                }

                _questions = value ?? new ObservableCollection<Question>();

                // 새 이벤트 구독
                _questions.CollectionChanged += (s, e) => SyncQuestionsToScoringAreas();
                foreach (var question in _questions)
                {
                    question.Options.CollectionChanged += (s, e) => SyncQuestionsToScoringAreas();
                }

                SyncQuestionsToScoringAreas();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 바코드 영역 오버레이 목록 (좌측에 위치, 수험번호/면접위원 번호 바코드 디코딩용)
        /// </summary>
        public ObservableCollection<RectangleOverlay> BarcodeAreas
        {
            get => _barcodeAreas;
            set
            {
                _barcodeAreas = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 템플릿의 기준 이미지 너비 (픽셀)
        /// </summary>
        public int ReferenceWidth
        {
            get => _referenceWidth;
            set
            {
                _referenceWidth = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 템플릿의 기준 이미지 높이 (픽셀)
        /// </summary>
        public int ReferenceHeight
        {
            get => _referenceHeight;
            set
            {
                _referenceHeight = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Questions의 변경사항을 ScoringAreas에 동기화합니다.
        /// </summary>
        private void SyncQuestionsToScoringAreas()
        {
            _scoringAreas.Clear();
            foreach (var question in _questions.OrderBy(q => q.QuestionNumber))
            {
                foreach (var option in question.Options)
                {
                    _scoringAreas.Add(option);
                }
            }
            OnPropertyChanged(nameof(ScoringAreas));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

