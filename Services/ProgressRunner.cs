using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SimpleOverlayEditor.Views;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// ProgressWindow + Task.Run + UI Dispatcher 마샬링 패턴을 공통화합니다.
    /// ViewModel의 긴 비동기/취소/진행률 코드를 단순화하기 위한 유틸성 서비스입니다.
    /// </summary>
    public static class ProgressRunner
    {
        public sealed class Scope
        {
            private readonly ProgressWindow _window;

            internal Scope(ProgressWindow window)
            {
                _window = window ?? throw new ArgumentNullException(nameof(window));
            }

            public CancellationToken CancellationToken => _window.CancellationToken;

            public void Report(int current, int total, string? statusMessage = null)
                => _window.UpdateProgress(current, total, statusMessage);

            public void Status(string message)
                => _window.UpdateStatus(message);

            public void Ui(Action action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                UiThread.Invoke(action);
            }
        }

        /// <summary>
        /// ProgressWindow를 표시한 뒤 work를 백그라운드에서 실행합니다.
        /// 취소 버튼/창 닫기는 CancellationToken으로 전달됩니다.
        /// </summary>
        /// <returns>취소되었으면 true, 정상 완료면 false</returns>
        public static async Task<bool> RunAsync(
            Window? owner,
            Func<Scope, Task> work,
            string? title = null,
            string? initialStatus = null)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            // owner가 null이거나 아직 로드되지 않은 경우 Owner를 설정하지 않음
            if (owner == null)
            {
                owner = Application.Current?.MainWindow;
            }

            var progressWindow = new ProgressWindow();
            
            // owner가 유효하고 로드된 경우에만 Owner 설정
            if (owner != null && owner.IsLoaded)
            {
                progressWindow.Owner = owner;
            }

            if (!string.IsNullOrEmpty(title))
            {
                progressWindow.Title = title;
            }

            progressWindow.Show();

            if (!string.IsNullOrEmpty(initialStatus))
            {
                progressWindow.UpdateStatus(initialStatus);
            }

            var scope = new Scope(progressWindow);

            try
            {
                await Task.Run(() => work(scope), scope.CancellationToken);
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
            finally
            {
                scope.Ui(() =>
                {
                    if (progressWindow.IsVisible)
                    {
                        progressWindow.Close();
                    }
                });
            }
        }
    }
}

