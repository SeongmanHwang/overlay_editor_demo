using System.Collections.Generic;
using System.Linq;

namespace SimpleOverlayEditor.Utils
{
    /// <summary>
    /// 실행 취소 관리자
    /// </summary>
    public class UndoManager
    {
        private readonly Stack<IUndoableCommand> _undoStack = new();
        private readonly Stack<IUndoableCommand> _redoStack = new();
        private const int MaxStackSize = 50; // 최대 스택 크기 제한

        /// <summary>
        /// 실행 취소 가능 여부
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// 다시 실행 가능 여부
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 명령 실행 및 스택에 추가
        /// </summary>
        public void ExecuteCommand(IUndoableCommand command)
        {
            if (command == null) return;

            command.Execute();
            _undoStack.Push(command);

            // 스택 크기 제한
            if (_undoStack.Count > MaxStackSize)
            {
                var commands = _undoStack.ToList();
                commands.RemoveAt(commands.Count - 1); // 가장 오래된 명령 제거
                _undoStack.Clear();
                foreach (var cmd in commands.Reverse<IUndoableCommand>())
                {
                    _undoStack.Push(cmd);
                }
            }

            // 새 명령 실행 시 Redo 스택 초기화
            _redoStack.Clear();
        }

        /// <summary>
        /// 실행 취소
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
        }

        /// <summary>
        /// 다시 실행
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);
        }

        /// <summary>
        /// 스택 초기화
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
