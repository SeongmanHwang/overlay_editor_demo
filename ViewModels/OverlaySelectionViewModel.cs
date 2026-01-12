using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 다중 선택된 오버레이를 관리하는 ViewModel
    /// 혼합값 표시 및 통일 기능 제공
    /// </summary>
    public sealed class OverlaySelectionViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<RectangleOverlay> _selected;

        public OverlaySelectionViewModel()
        {
            _selected = new ObservableCollection<RectangleOverlay>();
            _selected.CollectionChanged += (s, e) =>
            {
                // ✅ 핵심: Selected 속성 변경 통지 추가 (Single Source of Truth)
                OnPropertyChanged(nameof(Selected));
                
                OnPropertyChanged(nameof(IsMulti));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(nameof(X));
                OnPropertyChanged(nameof(Y));
                OnPropertyChanged(nameof(Width));
                OnPropertyChanged(nameof(Height));
                OnPropertyChanged(nameof(XDisplay));
                OnPropertyChanged(nameof(YDisplay));
                OnPropertyChanged(nameof(WidthDisplay));
                OnPropertyChanged(nameof(HeightDisplay));
            };
        }

        public ObservableCollection<RectangleOverlay> Selected => _selected;
        public bool IsMulti => _selected.Count > 1;
        public bool IsEmpty => _selected.Count == 0;
        public int Count => _selected.Count;

        // 각 오버레이의 PropertyChanged 구독
        private void SubscribeToOverlays()
        {
            foreach (var overlay in _selected)
            {
                overlay.PropertyChanged += Overlay_PropertyChanged;
            }
        }

        private void UnsubscribeFromOverlays()
        {
            foreach (var overlay in _selected)
            {
                overlay.PropertyChanged -= Overlay_PropertyChanged;
            }
        }

        private void Overlay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(RectangleOverlay.X)
                or nameof(RectangleOverlay.Y)
                or nameof(RectangleOverlay.Width)
                or nameof(RectangleOverlay.Height))
            {
                OnPropertyChanged(e.PropertyName);
                OnPropertyChanged($"{e.PropertyName}Display");
            }
        }

        // 표시용 속성: "다중 선택" 또는 숫자
        public string? XDisplay
        {
            get
            {
                if (_selected.Count == 0) return null;
                if (_selected.Count == 1) return _selected[0].X.ToString("F1");

                var value = GetCommonValue(o => o.X);
                return value.HasValue ? value.Value.ToString("F1") : "다중 선택";
            }
        }

        public string? YDisplay
        {
            get
            {
                if (_selected.Count == 0) return null;
                if (_selected.Count == 1) return _selected[0].Y.ToString("F1");

                var value = GetCommonValue(o => o.Y);
                return value.HasValue ? value.Value.ToString("F1") : "다중 선택";
            }
        }

        public string? WidthDisplay
        {
            get
            {
                if (_selected.Count == 0) return null;
                if (_selected.Count == 1) return _selected[0].Width.ToString("F1");

                var value = GetCommonValue(o => o.Width);
                return value.HasValue ? value.Value.ToString("F1") : "다중 선택";
            }
        }

        public string? HeightDisplay
        {
            get
            {
                if (_selected.Count == 0) return null;
                if (_selected.Count == 1) return _selected[0].Height.ToString("F1");

                var value = GetCommonValue(o => o.Height);
                return value.HasValue ? value.Value.ToString("F1") : "다중 선택";
            }
        }

        // 실제 값 속성 (통일 적용용)
        public double? X
        {
            get => GetCommonValue(o => o.X);
            set
            {
                if (value.HasValue)
                {
                    foreach (var overlay in _selected)
                    {
                        overlay.X = value.Value;
                    }
                }
            }
        }

        public double? Y
        {
            get => GetCommonValue(o => o.Y);
            set
            {
                if (value.HasValue)
                {
                    foreach (var overlay in _selected)
                    {
                        overlay.Y = value.Value;
                    }
                }
            }
        }

        public double? Width
        {
            get => GetCommonValue(o => o.Width);
            set
            {
                if (value.HasValue)
                {
                    foreach (var overlay in _selected)
                    {
                        overlay.Width = value.Value;
                    }
                }
            }
        }

        public double? Height
        {
            get => GetCommonValue(o => o.Height);
            set
            {
                if (value.HasValue)
                {
                    foreach (var overlay in _selected)
                    {
                        overlay.Height = value.Value;
                    }
                }
            }
        }

        private double? GetCommonValue(Func<RectangleOverlay, double> selector)
        {
            if (_selected.Count == 0) return null;
            if (_selected.Count == 1) return selector(_selected[0]);

            var first = selector(_selected[0]);
            return _selected.All(o => Math.Abs(selector(o) - first) < 0.001)
                ? first
                : null; // null = 혼합값
        }

        public void Add(RectangleOverlay overlay)
        {
            if (!_selected.Contains(overlay))
            {
                _selected.Add(overlay);
                overlay.PropertyChanged += Overlay_PropertyChanged;
            }
        }

        public void Remove(RectangleOverlay overlay)
        {
            if (_selected.Remove(overlay))
            {
                overlay.PropertyChanged -= Overlay_PropertyChanged;
            }
        }

        public void SetSelection(System.Collections.Generic.IEnumerable<RectangleOverlay> overlays)
        {
            UnsubscribeFromOverlays();
            _selected.Clear();
            foreach (var overlay in overlays)
            {
                _selected.Add(overlay);
            }
            SubscribeToOverlays();
            
            // ✅ Selected 속성 변경 통지 (CollectionChanged가 자동으로 발생하지만 명시적으로도)
            OnPropertyChanged(nameof(Selected));
            
            // Display 속성 업데이트
            OnPropertyChanged(nameof(XDisplay));
            OnPropertyChanged(nameof(YDisplay));
            OnPropertyChanged(nameof(WidthDisplay));
            OnPropertyChanged(nameof(HeightDisplay));
        }

        public void Clear()
        {
            UnsubscribeFromOverlays();
            _selected.Clear();
            // ✅ Selected 속성 변경 통지
            OnPropertyChanged(nameof(Selected));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
