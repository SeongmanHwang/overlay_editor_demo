using System;
using System.Threading;
using System.Windows;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// ProgressWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ProgressWindow : Window
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly bool _ownsCancellationTokenSource;
        private bool _isCancelling = false;

        public ProgressWindow(CancellationTokenSource? cancellationTokenSource = null)
        {
            InitializeComponent();

            if (cancellationTokenSource != null)
            {
                _cancellationTokenSource = cancellationTokenSource;
                _ownsCancellationTokenSource = false;
            }
            else
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _ownsCancellationTokenSource = true;
            }
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
            UiThread.Invoke(() =>
            {
                ProgressBar.Value = total > 0 ? (current * 100.0 / total) : 0;
                ProgressTextBlock.Text = $"{current} / {total} ({(total > 0 ? current * 100 / total : 0)}%)";
                if (statusMessage != null)
                {
                    StatusTextBlock.Text = statusMessage;
                }
            });
        }

        /// <summary>
        /// 상태 메시지를 업데이트합니다.
        /// </summary>
        public void UpdateStatus(string message)
        {
            UiThread.Invoke(() =>
            {
                StatusTextBlock.Text = message;
            });
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
            if (_ownsCancellationTokenSource)
            {
                _cancellationTokenSource?.Dispose();
            }
            base.OnClosed(e);
        }
    }
}
