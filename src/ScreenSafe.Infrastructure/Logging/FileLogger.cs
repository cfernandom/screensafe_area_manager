using System;
using System.IO;
using ScreenSafe.Domain;

namespace ScreenSafe.Infrastructure.Logging
{
    /// <summary>
    /// File-based logger. Writes formatted log entries to a specified log file.
    /// Auto-creates the directory if it does not exist.
    /// Format: yyyy-MM-dd HH:mm:ss [LEVEL] message
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logFilePath;

        /// <summary>
        /// Creates a new FileLogger that writes to the specified path.
        /// </summary>
        /// <param name="logFilePath">Full path to the log file.</param>
        public FileLogger(string logFilePath)
        {
            _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        }

        public void Info(string message) => Log(LogLevel.Info, message);

        public void Warning(string message) => Log(LogLevel.Warning, message);

        public void Error(string message) => Log(LogLevel.Error, message);

        public void Log(LogLevel level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var levelLabel = GetLevelLabel(level);
            var entry = $"{timestamp} [{levelLabel}] {message}";

            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.AppendAllText(_logFilePath, entry + Environment.NewLine);
        }

        private static string GetLevelLabel(LogLevel level) => level switch
        {
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARNING",
            LogLevel.Error => "ERROR",
            _ => "INFO"
        };
    }
}
