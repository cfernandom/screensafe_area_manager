using System;

namespace ScreenSafe.Domain
{
    /// <summary>
    /// Monitors Win32 desktop events (work area changes, display changes,
    /// Explorer restart) and raises corresponding C# events.
    /// </summary>
    public interface IWorkAreaWatcher
    {
        /// <summary>
        /// Raised when the work area has been changed (WM_SETTINGCHANGE with SPI_SETWORKAREA).
        /// </summary>
        event EventHandler WorkAreaChanged;

        /// <summary>
        /// Raised when the display resolution or configuration changes (WM_DISPLAYCHANGE).
        /// </summary>
        event EventHandler DisplayChanged;

        /// <summary>
        /// Raised when Explorer has been restarted (TaskbarCreated message).
        /// </summary>
        event EventHandler ExplorerRestarted;

        /// <summary>
        /// Starts the hidden window and Win32 message pump.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the message pump and destroys the hidden window.
        /// </summary>
        void Stop();
    }
}
