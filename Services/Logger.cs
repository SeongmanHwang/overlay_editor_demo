using System;
using System.IO;
using System.Text;

namespace SimpleOverlayEditor.Services
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public class Logger
    {
        private static Logger? _instance;
        private static readonly object _lock = new object();
        private readonly string _logFilePath;
        private readonly StringBuilder _logBuffer = new StringBuilder();

        private Logger()
        {
            var logDirectory = Path.Combine(PathService.AppDataFolder, "logs");
            Directory.CreateDirectory(logDirectory);
            
            var logFileName = $"overlay_editor_{DateTime.Now:yyyyMMdd}.log";
            _logFilePath = Path.Combine(logDirectory, logFileName);
        }

        public static Logger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new Logger();
                        }
                    }
                }
                return _instance;
            }
        }

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = level.ToString().ToUpper().PadRight(7);
            
            var logMessage = new StringBuilder();
            logMessage.AppendLine($"[{timestamp}] [{levelStr}] {message}");
            
            if (exception != null)
            {
                logMessage.AppendLine($"Exception: {exception.GetType().Name}");
                logMessage.AppendLine($"Message: {exception.Message}");
                logMessage.AppendLine($"Stack Trace:");
                logMessage.AppendLine(exception.StackTrace);
                if (exception.InnerException != null)
                {
                    logMessage.AppendLine($"Inner Exception: {exception.InnerException.Message}");
                }
            }

            var logEntry = logMessage.ToString();
            
            // 파일에 쓰기 (Visual Studio 없이도 작동)
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, logEntry, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // 로그 파일 쓰기 실패 시 콘솔에만 출력
                System.Diagnostics.Debug.WriteLine($"로그 파일 쓰기 실패: {ex.Message}");
            }
        }

        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);

        public string GetLogFilePath() => _logFilePath;
    }
}

