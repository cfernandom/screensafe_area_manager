namespace ScreenSafe.Domain
{
    /// <summary>
    /// Provides information about the current screen display dimensions.
    /// Uses P/Invoke to GetSystemMetrics to avoid WinForms dependency.
    /// </summary>
    public interface IScreenInfoProvider
    {
        /// <summary>
        /// Gets the full screen width in pixels.
        /// </summary>
        int GetScreenWidth();

        /// <summary>
        /// Gets the full screen height in pixels.
        /// </summary>
        int GetScreenHeight();
    }
}
