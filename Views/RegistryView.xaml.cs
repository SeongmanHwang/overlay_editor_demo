using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using SimpleOverlayEditor.Utils;

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

            DataGridMultiSortHelper.Apply(grid, view, e);
        }
    }
}
