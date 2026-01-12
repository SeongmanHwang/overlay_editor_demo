namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// OMR 템플릿 구조 관련 상수를 중앙화하여 관리합니다.
    /// 개발/빌드 단계에서 이 값들만 변경하면 다른 OMR 용지 구조에 대응할 수 있습니다.
    /// </summary>
    public static class OmrConstants
    {
        /// <summary>
        /// 문항 수 (기본값: 4)
        /// </summary>
        public static int QuestionsCount => 4;

        /// <summary>
        /// 문항당 선택지 수 (기본값: 12)
        /// </summary>
        public static int OptionsPerQuestion => 12;

        /// <summary>
        /// 총 채점 영역 수 (QuestionsCount × OptionsPerQuestion)
        /// </summary>
        public static int TotalScoringAreas => QuestionsCount * OptionsPerQuestion;
    }
}
