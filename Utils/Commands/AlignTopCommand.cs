using System.Collections.Generic;
using System.Linq;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Utils.Commands
{
    /// <summary>
    /// 위쪽 정렬 명령 (실행 취소 가능)
    /// </summary>
    public class AlignTopCommand : IUndoableCommand
    {
        private readonly List<RectangleOverlay> _overlays;
        private readonly Dictionary<RectangleOverlay, double> _originalY;

        public string Description => "위쪽에 맞추기";

        public AlignTopCommand(IEnumerable<RectangleOverlay> overlays)
        {
            _overlays = overlays.ToList();
            _originalY = _overlays.ToDictionary(o => o, o => o.Y);
        }

        public void Execute()
        {
            if (_overlays.Count < 2) return;
            var minY = _overlays.Min(o => o.Y);
            foreach (var overlay in _overlays)
            {
                overlay.Y = minY;
            }
        }

        public void Undo()
        {
            foreach (var overlay in _overlays)
            {
                if (_originalY.TryGetValue(overlay, out var originalY))
                {
                    overlay.Y = originalY;
                }
            }
        }
    }
}
