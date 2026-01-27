using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.ViewModels
{
    public partial class MarkingViewModel
    {
        /// <summary>
        /// 초기 정렬을 적용합니다 (View 레벨에서 정렬).
        /// 정렬 순서: 중복 데이터 -> 단순 오류 -> 정상 데이터 순서
        /// 각 그룹 내에서는 수험번호 -> 결합ID -> 파일명 순으로 정렬
        /// 
        /// 주의: 이 메서드는 초기 정렬만 설정합니다. 이후 사용자가 열 헤더를 클릭하면
        /// DataGrid가 자동으로 SortDescriptions를 관리하므로, 사용자의 정렬 변경이 누적됩니다.
        /// </summary>
        private void ApplyInitialSort()
        {
            if (FilteredSheetResults == null) return;

            // 기존 정렬이 없을 때만 초기 정렬 설정
            if (FilteredSheetResults.SortDescriptions.Count == 0)
            {
                // 1. IsDuplicate 내림차순 (중복이 먼저)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.IsDuplicate), 
                    System.ComponentModel.ListSortDirection.Descending));
                
                // 2. IsSimpleError 내림차순 (단순 오류가 그 다음)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.IsSimpleError), 
                    System.ComponentModel.ListSortDirection.Descending));
                
                // 3. StudentId 오름차순 (수험번호 순)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.StudentId), 
                    System.ComponentModel.ListSortDirection.Ascending));
                
                // 4. CombinedId 오름차순 (결합ID 순)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.CombinedId), 
                    System.ComponentModel.ListSortDirection.Ascending));
                
                // 5. ImageFileName 오름차순 (파일명 순)
                FilteredSheetResults.SortDescriptions.Add(
                    new System.ComponentModel.SortDescription(nameof(OmrSheetResult.ImageFileName), 
                    System.ComponentModel.ListSortDirection.Ascending));
            }
        }

        /// <summary>
        /// 필터 옵션을 초기화합니다.
        /// </summary>
        private void InitializeFilterOptions()
        {
            // 공통 유틸 사용: 시각/실/순 기본값
            SessionFilterOptions = OmrFilterUtils.CreateDefaultSessionOptions();
            SelectedSessionFilter = OmrFilterUtils.AllLabel;

            RoomFilterOptions = OmrFilterUtils.CreateDefaultAllOnlyOptions();
            SelectedRoomFilter = OmrFilterUtils.AllLabel;

            OrderFilterOptions = OmrFilterUtils.CreateDefaultAllOnlyOptions();
            SelectedOrderFilter = OmrFilterUtils.AllLabel;
        }

        /// <summary>
        /// 필터 옵션을 데이터에서 동적으로 업데이트합니다.
        /// </summary>
        private void UpdateFilterOptions()
        {
            if (SheetResults == null || SheetResults.Count == 0)
            {
                // 데이터가 없으면 기본값만 유지
                return;
            }

            // 실/순 옵션 동적 업데이트 (공통 유틸)
            OmrFilterUtils.UpdateNumericStringOptions(RoomFilterOptions, SheetResults.Select(r => r.RoomNumber));
            OmrFilterUtils.UpdateNumericStringOptions(OrderFilterOptions, SheetResults.Select(r => r.OrderNumber));

            // 현재 선택값이 유효한지 확인
            var selectedRoom = SelectedRoomFilter;
            OmrFilterUtils.EnsureSelectionIsValid(ref selectedRoom, RoomFilterOptions);
            SelectedRoomFilter = selectedRoom;

            var selectedOrder = SelectedOrderFilter;
            OmrFilterUtils.EnsureSelectionIsValid(ref selectedOrder, OrderFilterOptions);
            SelectedOrderFilter = selectedOrder;
        }

        /// <summary>
        /// 필터를 적용합니다.
        /// </summary>
        private void ApplyFilter()
        {
            if (FilteredSheetResults == null) return;

            FilteredSheetResults.Filter = item =>
            {
                if (item is not OmrSheetResult result) return false;

                // 라디오 필터 (전체/오류만/중복만)
                if (!OmrFilterUtils.PassesBaseFilter(_filterMode, result.IsSimpleError, result.IsDuplicate))
                    return false;

                if (!OmrFilterUtils.PassesSelectionFilter(SelectedSessionFilter, result.Session))
                    return false;
                if (!OmrFilterUtils.PassesSelectionFilter(SelectedRoomFilter, result.RoomNumber))
                    return false;
                if (!OmrFilterUtils.PassesSelectionFilter(SelectedOrderFilter, result.OrderNumber))
                    return false;

                return true;
            };

            FilteredSheetResults.Refresh();
        }
    }
}
