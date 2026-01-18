using System;
using System.Collections.Generic;
using System.Linq;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 결합ID(CombinedId) 기준 중복 검출 및 플래그/메시지 적용을 담당합니다.
    /// ViewModel 중복 로직 제거를 위한 공통 서비스입니다.
    /// </summary>
    public static class DuplicateDetector
    {
        /// <summary>
        /// CombinedId 기준으로 2개 이상 존재하는 그룹만 반환합니다.
        /// </summary>
        public static Dictionary<string, List<OmrSheetResult>> DetectCombinedIdDuplicates(
            IEnumerable<OmrSheetResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            return results
                .Where(r => !string.IsNullOrEmpty(r.CombinedId))
                .GroupBy(r => r.CombinedId!)
                .Where(g => g.Count() > 1)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// DetectCombinedIdDuplicates 결과를 기준으로 IsDuplicate/ErrorMessage를 적용합니다.
        /// 기존 동작과 동일하게 "결합ID 중복 (N개)" 메시지를 누적합니다.
        /// </summary>
        public static int ApplyCombinedIdDuplicates(
            IEnumerable<OmrSheetResult> results,
            IReadOnlyDictionary<string, List<OmrSheetResult>> groupedByCombinedId)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            if (groupedByCombinedId == null) throw new ArgumentNullException(nameof(groupedByCombinedId));

            var affected = 0;

            foreach (var result in results)
            {
                if (string.IsNullOrEmpty(result.CombinedId)) continue;
                if (!groupedByCombinedId.ContainsKey(result.CombinedId)) continue;

                // 기존 동작: 중복이면 플래그 세팅 + ErrorMessage에 중복 메시지 추가
                result.IsDuplicate = true;
                affected++;

                var duplicateCount = groupedByCombinedId[result.CombinedId].Count;
                var duplicateMessage = $"결합ID 중복 ({duplicateCount}개)";

                if (string.IsNullOrEmpty(result.ErrorMessage))
                {
                    result.ErrorMessage = duplicateMessage;
                }
                else
                {
                    result.ErrorMessage = result.ErrorMessage + "; " + duplicateMessage;
                }
            }

            return affected;
        }

        /// <summary>
        /// CombinedId 중복을 검출하고 즉시 적용합니다.
        /// </summary>
        public static Dictionary<string, List<OmrSheetResult>> DetectAndApplyCombinedIdDuplicates(
            IEnumerable<OmrSheetResult> results)
        {
            var groups = DetectCombinedIdDuplicates(results);
            ApplyCombinedIdDuplicates(results, groups);
            return groups;
        }
    }
}

