using System.Windows;
using System.Windows.Controls;
using SimpleOverlayEditor.ViewModels;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// HomeView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
            Loaded += HomeView_Loaded;
        }

        private void HomeView_Loaded(object sender, RoutedEventArgs e)
        {
            // View가 로드된 후 초기 데이터 로드
            if (DataContext is HomeViewModel viewModel && !string.IsNullOrEmpty(viewModel.SelectedRound))
            {
                // 약간의 지연을 두어 UI가 완전히 렌더링된 후 실행
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    viewModel.RefreshDataUsageCommand.Execute(null);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
    }
}










