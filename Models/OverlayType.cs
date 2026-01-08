namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// OMR 오버레이의 타입을 정의합니다.
    /// </summary>
    public enum OverlayType
    {
        /// <summary>
        /// 타이밍 마크 (상단에 위치, 이미지 정렬용)
        /// </summary>
        TimingMark,
        
        /// <summary>
        /// 채점 영역 (우측에 위치, 마킹 감지용)
        /// </summary>
        ScoringArea
    }
}

