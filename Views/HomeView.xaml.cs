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
            // Loaded 이벤트에서 자동 새로고침 제거
            // OnNavigatedTo에서 변경 여부 확인 후 필요시만 새로고침
        }
    }
}










