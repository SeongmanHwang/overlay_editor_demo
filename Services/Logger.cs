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
        private string _logFilePath;
        private DateTime _currentLogDate;
        private readonly string _logDirectory;
        private readonly StringBuilder _logBuffer = new StringBuilder();
        
        /// <summary>
        /// 최소 로그 레벨 (이 레벨 이상만 로깅)
        /// 성능 최적화: Debug 로그를 건너뛰려면 Info로 설정
        /// </summary>
        public LogLevel MinLogLevel { get; set; } = LogLevel.Info;

        private Logger()
        {
            _logDirectory = Path.Combine(PathService.AppDataFolder, "logs");
            Directory.CreateDirectory(_logDirectory);
            
            // 초기 로그 파일 경로 설정
            _currentLogDate = DateTime.Now.Date;
            UpdateLogFilePath();
            
            // 오래된 로그 파일 정리 (30일 이상 된 파일 삭제)
            CleanupOldLogFiles(_logDirectory);
        }
        
        /// <summary>
        /// 현재 날짜에 맞는 로그 파일 경로를 업데이트합니다.
        /// </summary>
        private void UpdateLogFilePath()
        {
            var logFileName = $"overlay_editor_{_currentLogDate:yyyyMMdd}.log";
            _logFilePath = Path.Combine(_logDirectory, logFileName);
        }
        
        /// <summary>
        /// 날짜가 바뀌었는지 확인하고 필요시 새 로그 파일로 전환합니다.
        /// </summary>
        private void CheckAndUpdateLogDate()
        {
            var today = DateTime.Now.Date;
            if (today != _currentLogDate)
            {
                _currentLogDate = today;
                UpdateLogFilePath();
            }
        }
        
        /// <summary>
        /// 오래된 로그 파일을 정리합니다 (30일 이상 된 파일 삭제)
        /// </summary>
        private void CleanupOldLogFiles(string logDirectory)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-30);
                var logFiles = Directory.GetFiles(logDirectory, "overlay_editor_*.log");
                
                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(logFile);
                        }
                        catch
                        {
                            // 삭제 실패해도 무시 (다른 프로세스가 사용 중일 수 있음)
                        }
                    }
                }
            }
            catch
            {
                // 정리 실패해도 무시
            }
        }
        
        /// <summary>
        /// 로그 파일 크기를 체크하고 필요시 로테이션합니다 (10MB 이상이면 백업 후 새 파일 생성)
        /// </summary>
        private void CheckAndRotateLogFile()
        {
            try
            {
                // 날짜가 바뀌었는지 먼저 확인
                CheckAndUpdateLogDate();
                
                if (File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    const long maxFileSize = 10 * 1024 * 1024; // 10MB
                    
                    if (fileInfo.Length >= maxFileSize)
                    {
                        // 같은 날짜 내에서 파일이 너무 커지면 번호를 붙여서 백업
                        var backupIndex = 1;
                        string backupPath;
                        do
                        {
                            var backupFileName = $"overlay_editor_{_currentLogDate:yyyyMMdd}_{backupIndex:D3}.log";
                            backupPath = Path.Combine(_logDirectory, backupFileName);
                            backupIndex++;
                        } while (File.Exists(backupPath));
                        
                        // 현재 로그 파일을 백업으로 이동
                        File.Move(_logFilePath, backupPath);
                        
                        // 새 로그 파일은 다음 Log 호출 시 자동 생성됨
                    }
                }
            }
            catch
            {
                // 로테이션 실패해도 무시 (로그 기록은 계속 진행)
            }
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
            // 성능 최적화: MinLogLevel보다 낮은 레벨의 로그는 건너뛰기
            if (level < MinLogLevel)
            {
                return;
            }
            
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
                    // 로그 파일 크기 제한 체크 (10MB 이상이면 로테이션)
                    CheckAndRotateLogFile();
                    
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

