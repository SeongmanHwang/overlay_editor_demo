using System;
using System.Windows;
using System.Windows.Threading;

namespace SimpleOverlayEditor.Utils
{
    /// <summary>
    /// UI 스레드 마샬링을 위한 공통 유틸입니다.
    /// ViewModel/Service에서 Dispatcher.Invoke 패턴을 직접 쓰지 않도록 돕습니다.
    /// </summary>
    public static class UiThread
    {
        public static Dispatcher? Dispatcher => Application.Current?.Dispatcher;

        public static void Invoke(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var dispatcher = Dispatcher;
            if (dispatcher == null)
            {
                // 테스트/비-UI 환경 등: 그냥 실행
                action();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                dispatcher.Invoke(action);
            }
        }
    }
}

