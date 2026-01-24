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
            private readonly CancellationTokenSource _cts;
            private readonly object _sync = new();
            private ProgressWindow? _window;

            private int _lastCurrent;
            private int _lastTotal;
            private string? _lastStatus;

            internal Scope(CancellationTokenSource cts)
            {
                _cts = cts ?? throw new ArgumentNullException(nameof(cts));
            }

            public CancellationToken CancellationToken => _cts.Token;

            public void Report(int current, int total, string? statusMessage = null)
            {
                lock (_sync)
                {
                    _lastCurrent = current;
                    _lastTotal = total;
                    if (statusMessage != null) _lastStatus = statusMessage;
                }

                _window?.UpdateProgress(current, total, statusMessage);
            }

            public void Status(string message)
            {
                lock (_sync)
                {
                    _lastStatus = message;
                }

                _window?.UpdateStatus(message);
            }

            public void Ui(Action action)
            {
                if (action == null) throw new ArgumentNullException(nameof(action));
                UiThread.Invoke(action);
            }

            internal void AttachWindow(ProgressWindow window, string? title, string? initialStatus)
            {
                if (window == null) throw new ArgumentNullException(nameof(window));

                _window = window;

                if (!string.IsNullOrEmpty(title))
                {
                    _window.Title = title;
                }

                if (!string.IsNullOrEmpty(initialStatus))
                {
                    _window.UpdateStatus(initialStatus);
                }

                int current, total;
                string? status;
                lock (_sync)
                {
                    current = _lastCurrent;
                    total = _lastTotal;
                    status = _lastStatus;
                }

                // 작업이 창 표시 전에 이미 진행률/상태를 보고했을 수 있으므로 마지막 값을 반영
                if (current != 0 || total != 0 || status != null)
                {
                    _window.UpdateProgress(current, total, status);
                }
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
            string? initialStatus = null,
            int showDelayMs = 1000)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            // owner가 null이거나 아직 로드되지 않은 경우 Owner를 설정하지 않음
            if (owner == null)
            {
                owner = Application.Current?.MainWindow;
            }

            var cts = new CancellationTokenSource();
            var scope = new Scope(cts);
            ProgressWindow? progressWindow = null;

            try
            {
                var workTask = Task.Run(() => work(scope), scope.CancellationToken);

                if (showDelayMs < 0) showDelayMs = 0;

                if (showDelayMs == 0)
                {
                    UiThread.Invoke(() =>
                    {
                        progressWindow = new ProgressWindow(cts);

                        // owner가 유효하고 로드된 경우에만 Owner 설정
                        if (owner != null && owner.IsLoaded)
                        {
                            progressWindow.Owner = owner;
                        }

                        progressWindow.Show();
                        scope.AttachWindow(progressWindow, title, initialStatus);
                    });
                }
                else
                {
                    var delayTask = Task.Delay(showDelayMs);
                    var first = await Task.WhenAny(workTask, delayTask);

                    if (first == delayTask && !workTask.IsCompleted)
                    {
                        UiThread.Invoke(() =>
                        {
                            progressWindow = new ProgressWindow(cts);

                            // owner가 유효하고 로드된 경우에만 Owner 설정
                            if (owner != null && owner.IsLoaded)
                            {
                                progressWindow.Owner = owner;
                            }

                            progressWindow.Show();
                            scope.AttachWindow(progressWindow, title, initialStatus);
                        });
                    }
                }

                await workTask;
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
                    if (progressWindow?.IsVisible == true)
                    {
                        progressWindow.Close();
                    }
                });

                cts.Dispose();
            }
        }
    }
}

