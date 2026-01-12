namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 
    /// 
    /// 
    /// 애플리케이션의 모드를 정의합니다.
    /// </summary>
    public enum ApplicationMode
    {
        /// <summary>
        /// 홈 화면
        /// </summary>
        Home,

        /// <summary>
        /// 템플릿 편집 모드 (템플릿 제작 및 수정)
        /// </summary>
        TemplateEdit,

        /// <summary>
        /// 마킹 리딩 모드 (OMR 마킹 리딩 및 분석)
        /// </summary>
        Marking,

        /// <summary>
        /// 수험생 명렬 관리 모드
        /// </summary>
        Registry,

        /// <summary>
        /// 채점 및 성적 처리 모드
        /// </summary>
        Grading,

        /// <summary>
        /// 정답 및 배점 모드 (문항별 선택지 점수 설정)
        /// </summary>
        ScoringRule,

        /// <summary>
        /// 수기 검산 모드 (프로그램 결과를 사람이 직접 확인)
        /// </summary>
        ManualVerification
    }
}