using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Utils;
using SimpleOverlayEditor.ViewModels;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// GradingView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class GradingView : UserControl
    {
        private GradingViewModel ViewModel => (GradingViewModel)DataContext;

        public GradingView()
        {
            InitializeComponent();
        }

        private void GradingDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (ViewModel?.FilteredGradingResults == null) return;

            var grid = (DataGrid)sender;
            var view = ViewModel.FilteredGradingResults;

            DataGridMultiSortHelper.Apply(grid, view, e, onUserSorted: ViewModel.MarkUserHasSorted);
        }

        private void GradingDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null) return;

            if (sender is not DataGrid grid) return;
            if (grid.SelectedItem is not GradingResult selected) return;
            if (string.IsNullOrWhiteSpace(selected.StudentId)) return;

            ViewModel.Navigation.NavigateTo(SimpleOverlayEditor.Models.ApplicationMode.SingleStudentVerification, selected.StudentId);
        }
    }
}
