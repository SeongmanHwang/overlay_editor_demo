using System.Collections.ObjectModel;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Utils.Commands
{
    /// <summary>
    /// 오버레이 추가 명령 (실행 취소 가능)
    /// </summary>
    public class AddOverlayCommand : IUndoableCommand
    {
        private readonly RectangleOverlay _overlay;
        private readonly ObservableCollection<RectangleOverlay> _collection;

        public string Description => $"오버레이 추가 ({_overlay.OverlayType})";

        public AddOverlayCommand(
            RectangleOverlay overlay,
            ObservableCollection<RectangleOverlay> collection)
        {
            _overlay = overlay;
            _collection = collection;
        }

        public void Execute()
        {
            _collection.Add(_overlay);
        }

        public void Undo()
        {
            _collection.Remove(_overlay);
        }
    }
}
