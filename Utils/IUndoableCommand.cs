namespace SimpleOverlayEditor.Utils
{
    /// <summary>
    /// 실행 취소 가능한 명령 인터페이스
    /// </summary>
    public interface IUndoableCommand
    {
        /// <summary>
        /// 명령 실행
        /// </summary>
        void Execute();

        /// <summary>
        /// 명령 실행 취소
        /// </summary>
        void Undo();

        /// <summary>
        /// 명령 설명 (디버깅/로깅용)
        /// </summary>
        string Description { get; }
    }
}
