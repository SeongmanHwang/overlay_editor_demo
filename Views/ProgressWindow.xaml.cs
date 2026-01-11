using System;
using System.Threading;
using System.Windows;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// ProgressWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ProgressWindow : Window
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isCancelling = false;

        public ProgressWindow()
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 취소 토큰을 가져옵니다.
        /// </summary>
        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

        /// <summary>
        /// 취소되었는지 확인합니다.
        /// </summary>
        public bool IsCancelled => _cancellationTokenSource?.IsCancellationRequested ?? false;

        /// <summary>
        /// 진행률을 업데이트합니다 (0-100).
        /// </summary>
        public void UpdateProgress(int current, int total, string? statusMessage = null)
        {
            if (Dispatcher.CheckAccess())
            {
                ProgressBar.Value = total > 0 ? (current * 100.0 / total) : 0;
                ProgressTextBlock.Text = $"{current} / {total} ({(total > 0 ? current * 100 / total : 0)}%)";
                if (statusMessage != null)
                {
                    StatusTextBlock.Text = statusMessage;
                }
            }
            else
            {
                Dispatcher.Invoke(() => UpdateProgress(current, total, statusMessage));
            }
        }

        /// <summary>
        /// 상태 메시지를 업데이트합니다.
        /// </summary>
        public void UpdateStatus(string message)
        {
            if (Dispatcher.CheckAccess())
            {
                StatusTextBlock.Text = message;
            }
            else
            {
                Dispatcher.Invoke(() => UpdateStatus(message));
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCancelling)
            {
                _isCancelling = true;
                _cancellationTokenSource?.Cancel();
                CancelButton.IsEnabled = false;
                StatusTextBlock.Text = "취소 중...";
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 창을 닫을 때도 취소 처리
            if (!_isCancelling && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _isCancelling = true;
                _cancellationTokenSource.Cancel();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _cancellationTokenSource?.Dispose();
            base.OnClosed(e);
        }
    }
}
