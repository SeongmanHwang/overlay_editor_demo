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
        ScoringArea,
        
        /// <summary>
        /// 바코드 영역 (좌측에 위치, 수험번호/면접위원 번호 바코드 디코딩용)
        /// </summary>
        BarcodeArea
    }
}

