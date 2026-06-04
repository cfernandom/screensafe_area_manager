namespace ScreenSafe.Domain
{
    /// <summary>
    /// Provides daemon running status by checking for the named mutex.
    /// Abstracts Win32 P/Invoke for testability.
    /// </summary>
    public interface IDaemonStatusProvider
    {
        /// <summary>
        /// Returns true if the ScreenSafe daemon mutex exists (daemon is running).
        /// </summary>
        bool IsDaemonRunning();
    }
}
