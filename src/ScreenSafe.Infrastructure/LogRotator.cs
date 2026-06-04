using System;
using System.IO;
using System.Threading;
using ScreenSafe.Domain;

namespace ScreenSafe.Infrastructure
{
    /// <summary>
    /// Size-based log file rotator. Writes log entries to sequentially numbered files
    /// (screensafe-{yyyy-MM-dd}-{n}.log) and rotates when the current file exceeds the
    /// configured maximum size. Retains at most maxRetainedFiles files; older files are
    /// deleted when new ones are created.
    /// Thread-safe: uses ReaderWriterLockSlim for write serialization.
    /// </summary>
    public class LogRotator
    {
        private readonly string _logDirectory;
        private readonly long _maxFileSizeBytes;
        private readonly int _maxRetainedFiles;
        private readonly ILogger? _logger;
        private readonly ReaderWriterLockSlim _lock = new();
        private int _currentFileIndex;

        /// <summary>
        /// Creates a new LogRotator.
        /// </summary>
        /// <param name="logDirectory">Directory for log file storage. Auto-created if not found.</param>
        /// <param name="maxFileSizeBytes">Maximum file size in bytes before rotation. Default 1MB (1,048,576).</param>
        /// <param name="maxRetainedFiles">Maximum number of log files to retain. Default 3.</param>
        /// <param name="logger">Optional ILogger for internal diagnostics.</param>
        public LogRotator(
            string logDirectory,
            long maxFileSizeBytes = 1048576,
            int maxRetainedFiles = 3,
            ILogger? logger = null)
        {
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            _maxFileSizeBytes = maxFileSizeBytes;
            _maxRetainedFiles = maxRetainedFiles;
            _logger = logger;
            _currentFileIndex = 0;

            EnsureDirectory();
        }

        /// <summary>
        /// Writes a formatted log entry to the current log file.
        /// Rotates to a new file if the current one exceeds the maximum size.
        /// Format: yyyy-MM-dd HH:mm:ss [LEVEL] message
        /// </summary>
        /// <param name="level">Log level label (e.g., INFO, WARNING, ERROR).</param>
        /// <param name="message">Log message content.</param>
        public void Write(string level, string message)
        {
            _lock.EnterWriteLock();
            try
            {
                var currentPath = GetCurrentFilePath();
                var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";

                if (File.Exists(currentPath))
                {
                    var fileInfo = new FileInfo(currentPath);
                    if (fileInfo.Length >= _maxFileSizeBytes)
                    {
                        Rotate();
                        currentPath = GetCurrentFilePath();
                    }
                }

                File.AppendAllText(currentPath, entry + Environment.NewLine);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Rotates to the next file index and deletes the oldest file if retention is exceeded.
        /// </summary>
        private void Rotate()
        {
            _currentFileIndex++;

            // If we've exceeded the retention limit, delete the oldest file
            if (_currentFileIndex >= _maxRetainedFiles)
            {
                var oldestIndex = _currentFileIndex - _maxRetainedFiles;
                var oldestPath = GetFilePathForIndex(oldestIndex);

                if (File.Exists(oldestPath))
                {
                    try
                    {
                        File.Delete(oldestPath);
                        _logger?.Info($"Deleted old log file: {oldestPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"Failed to delete old log file: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Returns the full path for the current file index.
        /// </summary>
        private string GetCurrentFilePath()
        {
            return GetFilePathForIndex(_currentFileIndex);
        }

        /// <summary>
        /// Returns the full path for a specific file index.
        /// Format: screensafe-{yyyy-MM-dd}-{n}.log
        /// </summary>
        private string GetFilePathForIndex(int index)
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var fileName = $"screensafe-{today}-{index}.log";
            return Path.Combine(_logDirectory, fileName);
        }

        /// <summary>
        /// Ensures the log directory exists, creating it if necessary.
        /// </summary>
        private void EnsureDirectory()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }
    }
}
