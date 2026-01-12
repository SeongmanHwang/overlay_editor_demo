using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// RegistryView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class RegistryView : UserControl
    {
        public RegistryView()
        {
            InitializeComponent();
        }

        private void StudentDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            HandleSorting(sender, e);
        }

        private void InterviewerDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            HandleSorting(sender, e);
        }

        private void HandleSorting(object sender, DataGridSortingEventArgs e)
        {
            var grid = (DataGrid)sender;
            
            // DataGrid의 ItemsSource에서 ICollectionView 가져오기
            var itemsSource = grid.ItemsSource;
            if (itemsSource == null) return;

            var view = CollectionViewSource.GetDefaultView(itemsSource);
            if (view == null) return;

            e.Handled = true;

            var key = e.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(key)) return;

            // 현재 정렬 목록을 복사
            var current = view.SortDescriptions.ToList();

            // 클릭한 열의 위치 찾기
            var existingIndex = current.FindIndex(sd => sd.PropertyName == key);
            bool isPrimary = existingIndex == 0;
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
