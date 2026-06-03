namespace ScreenSafe.Domain
{
    /// <summary>
    /// Application settings persisted in appsettings.json.
    /// </summary>
    public sealed class AppSettings
    {
        /// <summary>
        /// Whether the work area reservation is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Number of pixels to reserve at the bottom of the screen.
        /// </summary>
        public int ReservedBottomPixels { get; set; } = 80;

        /// <summary>
        /// The original full-screen work area before any reservation.
        /// Null when no reservation is active.
        /// </summary>
        public ScreenRect? OriginalWorkArea { get; set; }

        /// <summary>
        /// Strategy to use: "SpSetWorkArea", "ShAppBarMessage", or "auto".
        /// </summary>
        public string Strategy { get; set; } = "auto";

        /// <summary>
        /// Directory path for log file storage.
        /// Default: %LOCALAPPDATA%\ScreenSafe\Logs\
        /// </summary>
        public string LogPath { get; set; } = string.Empty;
    }
}
