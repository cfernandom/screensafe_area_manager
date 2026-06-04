namespace ScreenSafe.Domain
{
    /// <summary>
    /// Structured diagnostic data for the health command output.
    /// Aggregates state from multiple sources: screen info, settings,
    /// work area manager, daemon status, and auto-start registration.
    /// </summary>
    public class HealthReport
    {
        /// <summary>Current screen width in pixels.</summary>
        public int ScreenWidth { get; set; }

        /// <summary>Current screen height in pixels.</summary>
        public int ScreenHeight { get; set; }

        /// <summary>The desired work area bounds from configuration.</summary>
        public (int left, int top, int right, int bottom) DesiredWorkArea { get; set; }

        /// <summary>The current actual work area, or null if it cannot be read.</summary>
        public (int left, int top, int right, int bottom)? CurrentWorkArea { get; set; }

        /// <summary>The active strategy name (e.g., "spisetworkarea", "auto").</summary>
        public string Strategy { get; set; } = string.Empty;

        /// <summary>Whether the daemon is currently running (based on named mutex).</summary>
        public bool DaemonRunning { get; set; }

        /// <summary>Whether auto-start is enabled in the Windows registry Run key.</summary>
        public bool AutoStartEnabled { get; set; }

        /// <summary>ISO 8601 timestamp of the last reapply, or "N/A" if none occurred.</summary>
        public string LastReapply { get; set; } = "N/A";

        /// <summary>Health status string: OK, Mismatch Detected, Daemon Not Running, Error Reading State.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Exit code: 0=success, 1=daemon not running, 2=error reading state.</summary>
        public int ExitCode { get; set; }
    }
}
