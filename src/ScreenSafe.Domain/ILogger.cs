namespace ScreenSafe.Domain
{
    /// <summary>
    /// Log levels supported by the logger infrastructure.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Lifecycle and event information.</summary>
        Info,

        /// <summary>Detected changes and reapplies.</summary>
        Warning,

        /// <summary>Failures and critical errors.</summary>
        Error
    }

    /// <summary>
    /// Abstraction for logging with level-based severity.
    /// Implementations write to console, file, or other targets.
    /// </summary>
    public interface ILogger
    {
        /// <summary>Logs an informational message.</summary>
        void Info(string message);

        /// <summary>Logs a warning message.</summary>
        void Warning(string message);

        /// <summary>Logs an error message.</summary>
        void Error(string message);

        /// <summary>Logs a message at the specified log level.</summary>
        void Log(LogLevel level, string message);
    }
}
