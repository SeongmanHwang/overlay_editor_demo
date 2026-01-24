using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace SimpleOverlayEditor.Utils
{
    /// <summary>
    /// DataGrid Sorting 이벤트에서 멀티키 정렬 UX를 공통으로 처리합니다.
    /// - 클릭한 열을 1순위 정렬 키로 올리고, 기존 정렬 키는 차순위로 유지
    /// - UI 정렬 아이콘은 1순위만 표시
    /// </summary>
    public static class DataGridMultiSortHelper
    {
        public static void Apply(
            DataGrid grid,
            ICollectionView view,
            DataGridSortingEventArgs e,
            Action? onUserSorted = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (e == null) throw new ArgumentNullException(nameof(e));

            e.Handled = true; // 기본 정렬 막기

            var key = e.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(key)) return;

            onUserSorted?.Invoke();

            // 현재 정렬 목록을 복사
            var current = view.SortDescriptions.ToList();

            // 클릭한 열의 위치 찾기
            var existingIndex = current.FindIndex(sd => sd.PropertyName == key);
            bool isPrimary = existingIndex == 0;  // 0이면 1순위
            bool existsSomewhere = existingIndex >= 0;

            ListSortDirection newDir;

            if (isPrimary)
            {
                // 1순위 열을 다시 클릭: 토글
                newDir = current[0].Direction == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else if (existsSomewhere)
            {
                // 2순위 이하로 있던 열을 1순위로 올림: 방향 유지
                newDir = current[existingIndex].Direction;
            }
            else
            {
                // 처음 등장한 열: 항상 오름차순
                newDir = ListSortDirection.Ascending;
            }

            // 클릭한 열을 제거한 나머지(방향 포함 그대로 유지)
            var rest = current.Where(sd => sd.PropertyName != key).ToList();

            // 클릭한 열을 1순위로 올리고, 기존 정렬들은 차순위로 유지
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(key, newDir));
            foreach (var sd in rest)
                view.SortDescriptions.Add(sd);

            view.Refresh();

            // UI 아이콘은 1순위만 표시
            foreach (var col in grid.Columns)
                col.SortDirection = null;

            e.Column.SortDirection = newDir;
        }
    }
}

