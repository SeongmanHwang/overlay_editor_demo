using System.Collections.ObjectModel;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Utils.Commands
{
    /// <summary>
    /// 오버레이 삭제 명령 (실행 취소 가능)
    /// </summary>
    public class DeleteOverlayCommand : IUndoableCommand
    {
        private readonly RectangleOverlay _overlay;
        private readonly ObservableCollection<RectangleOverlay> _collection;
        private readonly int _originalIndex;
        private readonly OverlayType _overlayType;
        private readonly Question? _parentQuestion; // ScoringArea일 경우 부모 Question

        public string Description => $"오버레이 삭제 ({_overlayType})";

        public DeleteOverlayCommand(
            RectangleOverlay overlay,
            ObservableCollection<RectangleOverlay> collection,
            OverlayType overlayType,
            Question? parentQuestion = null)
        {
            _overlay = overlay;
            _collection = collection;
            _originalIndex = collection.IndexOf(overlay);
            _overlayType = overlayType;
            _parentQuestion = parentQuestion;
        }

        public void Execute()
        {
            _collection.Remove(_overlay);
        }

        public void Undo()
        {
            // 원래 위치에 복원
            if (_originalIndex >= 0 && _originalIndex <= _collection.Count)
            {
                _collection.Insert(_originalIndex, _overlay);
            }
            else
            {
                _collection.Add(_overlay);
            }
        }
    }
}
