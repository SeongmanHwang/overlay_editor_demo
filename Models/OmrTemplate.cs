using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
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
            // TimingMarks 초기화 ({OmrConstants.TimingMarksCount}개 슬롯)
            for (int i = 1; i <= OmrConstants.TimingMarksCount; i++)
            {
                _timingMarks.Add(new RectangleOverlay
                {
                    OverlayType = OverlayType.TimingMark,
                    OptionNumber = i,
                    QuestionNumber = null,
                    Width = 0,
                    Height = 0
                });
            }

            // BarcodeAreas 초기화 ({OmrConstants.BarcodeAreasCount}개 슬롯)
            for (int i = 1; i <= OmrConstants.BarcodeAreasCount; i++)
            {
                _barcodeAreas.Add(new RectangleOverlay
                {
                    OverlayType = OverlayType.BarcodeArea,
                    OptionNumber = i,
                    QuestionNumber = null,
                    Width = 0,
                    Height = 0
                });
            }

            // Questions 초기화 ({OmrConstants.QuestionsCount}개 문항)
            for (int i = 1; i <= OmrConstants.QuestionsCount; i++)
            {
                var question = new Question { QuestionNumber = i };
                SubscribeQuestion(question);
                _questions.Add(question);
            }

            // Questions 변경 시 ScoringAreas 자동 동기화
            _questions.CollectionChanged += Questions_CollectionChanged;

            // 초기 동기화
            SyncQuestionsToScoringAreas();
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
        /// 채점 영역 오버레이 목록 (우측에 위치, 마킹 리딩용)
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
        /// 문항 목록 ({OmrConstants.QuestionsCount}개 문항, 각 {OmrConstants.OptionsPerQuestion}개 선택지)
        /// </summary>
        public ObservableCollection<Question> Questions
        {
            get => _questions;
            set
            {
                if (_questions != null)
                {
                    UnsubscribeQuestions(_questions);
                    _questions.CollectionChanged -= Questions_CollectionChanged;
                }

                _questions = value ?? new ObservableCollection<Question>();

                SubscribeQuestions(_questions);
                _questions.CollectionChanged += Questions_CollectionChanged;

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
        /// 배치된 슬롯(IsPlaced == true)만 ScoringAreas에 추가합니다.
        /// </summary>
        private void SyncQuestionsToScoringAreas()
        {
            _scoringAreas.Clear();
            foreach (var question in _questions.OrderBy(q => q.QuestionNumber))
            {
                foreach (var option in question.Options.OrderBy(o => o.OptionNumber))
                {
                    // 고정 슬롯 구조: 항상 포함 (48개 고정)
                    _scoringAreas.Add(option);
                }
            }
            OnPropertyChanged(nameof(ScoringAreas));
        }

        private void Questions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (Question question in e.OldItems)
                {
                    UnsubscribeQuestion(question);
                }
            }

            if (e.NewItems != null)
            {
                foreach (Question question in e.NewItems)
                {
                    SubscribeQuestion(question);
                }
            }

            SyncQuestionsToScoringAreas();
        }

        private void SubscribeQuestions(ObservableCollection<Question> questions)
        {
            foreach (var question in questions)
            {
                SubscribeQuestion(question);
            }
        }

        private void UnsubscribeQuestions(ObservableCollection<Question> questions)
        {
            foreach (var question in questions)
            {
                UnsubscribeQuestion(question);
            }
        }

        private void SubscribeQuestion(Question question)
        {
            question.Options.CollectionChanged += QuestionOptions_CollectionChanged;
            foreach (var option in question.Options)
            {
                option.PropertyChanged += OptionOverlay_PropertyChanged;
            }
        }

        private void UnsubscribeQuestion(Question question)
        {
            question.Options.CollectionChanged -= QuestionOptions_CollectionChanged;
            foreach (var option in question.Options)
            {
                option.PropertyChanged -= OptionOverlay_PropertyChanged;
            }
        }

        private void QuestionOptions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (RectangleOverlay option in e.OldItems)
                {
                    option.PropertyChanged -= OptionOverlay_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (RectangleOverlay option in e.NewItems)
                {
                    option.PropertyChanged += OptionOverlay_PropertyChanged;
                }
            }

            SyncQuestionsToScoringAreas();
        }

        private void OptionOverlay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // IsPlaced에 영향을 주는 속성 변경 시 ScoringAreas 재구성
            if (e.PropertyName is nameof(RectangleOverlay.Width)
                or nameof(RectangleOverlay.Height)
                or nameof(RectangleOverlay.OptionNumber))
            {
                SyncQuestionsToScoringAreas();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

