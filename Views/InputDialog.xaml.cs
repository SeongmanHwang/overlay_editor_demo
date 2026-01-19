using System.Windows;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// InputDialog.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class InputDialog : Window
    {
        public string? Result { get; private set; }

        public InputDialog(string message, string title = "입력", string defaultValue = "")
        {
            InitializeComponent();
            Title = title;
            MessageTextBlock.Text = message;
            InputTextBox.Text = defaultValue;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // 필요시 유효성 검사 로직 추가 가능
        }
    }
}
