using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SimpleOverlayEditor.Utils
{
    /// <summary>
    /// Marking/Grading 화면에서 공통으로 사용하는 필터(시각/실/순 + 오류/중복 라디오) 유틸입니다.
    /// </summary>
    public static class OmrFilterUtils
    {
        public const string AllLabel = "전체";

        public static ObservableCollection<string> CreateDefaultSessionOptions()
            => new ObservableCollection<string> { AllLabel, "오전", "오후" };

        public static ObservableCollection<string> CreateDefaultAllOnlyOptions()
            => new ObservableCollection<string> { AllLabel };

        public static void UpdateNumericStringOptions(
            ObservableCollection<string> target,
            IEnumerable<string?> values)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (values == null) throw new ArgumentNullException(nameof(values));

            var items = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct()
                .OrderBy(v => int.TryParse(v, out var num) ? num : int.MaxValue)
                .ToList();

            target.Clear();
            target.Add(AllLabel);
            foreach (var item in items)
            {
                target.Add(item);
            }
        }

        public static void EnsureSelectionIsValid(
            ref string? selected,
            ObservableCollection<string> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (selected == null)
            {
                selected = AllLabel;
                return;
            }

            if (!options.Contains(selected))
            {
                selected = AllLabel;
            }
        }

        public static bool PassesBaseFilter(string filterMode, bool isSimpleError, bool isDuplicate)
        {
            return filterMode switch
            {
                "Errors" => isSimpleError,    // 단순 오류만 (중복 제외)
                "Duplicates" => isDuplicate,  // 중복만
                "All" => true,
                _ => true
            };
        }

        public static bool PassesSelectionFilter(string? selected, string? actual)
        {
            if (string.IsNullOrEmpty(selected) || selected == AllLabel) return true;
            return actual == selected;
        }
    }
}

