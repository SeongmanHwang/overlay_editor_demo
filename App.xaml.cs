using System;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace SimpleOverlayEditor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SimpleOverlayEditor.Services.Logger.Instance.Info("=== 애플리케이션 시작 ===");
            SimpleOverlayEditor.Services.Logger.Instance.Info($"로그 파일 위치: {SimpleOverlayEditor.Services.Logger.Instance.GetLogFilePath()}");

            // AppData 정리 (캐시/출력 폴더 용량 폭증 방지)
            // ✅ UI 프리즈 방지: 대용량 폴더(수만~수십만 파일) 열거가 오래 걸릴 수 있으므로 백그라운드에서 수행
            _ = Task.Run(() =>
            {
                try
                {
                    SimpleOverlayEditor.Services.PathService.CleanupAppData();
                    SimpleOverlayEditor.Services.Logger.Instance.Info("AppData 캐시/출력 폴더 정리 완료");
                }
                catch (Exception ex)
                {
                    // 정리 실패는 치명적이지 않음
                    SimpleOverlayEditor.Services.Logger.Instance.Warning($"AppData 정리 중 오류: {ex.Message}");
                }
            });

            // 처리되지 않은 예외 처리
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            SimpleOverlayEditor.Services.Logger.Instance.Error("처리되지 않은 예외 발생 (Dispatcher)", e.Exception);
            
            MessageBox.Show(
                $"처리되지 않은 예외가 발생했습니다:\n\n{e.Exception.Message}\n\n로그 파일을 확인하세요:\n{SimpleOverlayEditor.Services.Logger.Instance.GetLogFilePath()}",
                "오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                SimpleOverlayEditor.Services.Logger.Instance.Error("치명적인 오류 발생 (AppDomain)", ex);
                
                MessageBox.Show(
                    $"치명적인 오류가 발생했습니다:\n\n{ex.Message}\n\n로그 파일을 확인하세요:\n{SimpleOverlayEditor.Services.Logger.Instance.GetLogFilePath()}",
                    "치명적 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}

