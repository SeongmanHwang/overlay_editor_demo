using System.Collections.Generic;
using System.Linq;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Utils.Commands
{
    /// <summary>
    /// 왼쪽 정렬 명령 (실행 취소 가능)
    /// </summary>
    public class AlignLeftCommand : IUndoableCommand
    {
        private readonly List<RectangleOverlay> _overlays;
        private readonly Dictionary<RectangleOverlay, double> _originalX;

        public string Description => "왼쪽에 맞추기";

        public AlignLeftCommand(IEnumerable<RectangleOverlay> overlays)
        {
            _overlays = overlays.ToList();
            _originalX = _overlays.ToDictionary(o => o, o => o.X);
        }

        public void Execute()
        {
            if (_overlays.Count < 2) return;
            var minX = _overlays.Min(o => o.X);
            foreach (var overlay in _overlays)
            {
                overlay.X = minX;
            }
        }

        public void Undo()
        {
            foreach (var overlay in _overlays)
            {
                if (_originalX.TryGetValue(overlay, out var originalX))
                {
                    overlay.X = originalX;
                }
            }
        }
    }
}
