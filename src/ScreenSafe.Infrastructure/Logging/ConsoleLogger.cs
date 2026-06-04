using System;
using ScreenSafe.Domain;

namespace ScreenSafe.Infrastructure.Logging
{
    /// <summary>
    /// Console-based logger. Writes formatted log output to stdout
    /// with level-specific coloring: Info=gray, Warning=yellow, Error=red.
    /// Format: [timestamp] [LEVEL] message
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        public void Info(string message) => Log(LogLevel.Info, message);

        public void Warning(string message) => Log(LogLevel.Warning, message);

        public void Error(string message) => Log(LogLevel.Error, message);

        public void Log(LogLevel level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var levelLabel = GetLevelLabel(level);
            var color = GetColor(level);
            var originalColor = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"{timestamp} [{levelLabel}] {message}");
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        private static string GetLevelLabel(LogLevel level) => level switch
        {
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARNING",
            LogLevel.Error => "ERROR",
            _ => "INFO"
        };

        private static ConsoleColor GetColor(LogLevel level) => level switch
        {
            LogLevel.Info => ConsoleColor.Gray,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
    }
}
