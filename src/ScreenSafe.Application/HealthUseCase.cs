using System;
using ScreenSafe.Domain;

namespace ScreenSafe.Application
{
    /// <summary>
    /// Use case for aggregating diagnostic health data from multiple sources.
    /// Returns a <see cref="HealthReport"/> with structured information about
    /// the current screen configuration, daemon status, and work area state.
    /// </summary>
    public class HealthUseCase
    {
        private readonly IScreenInfoProvider _screenInfo;
        private readonly IWorkAreaManager _workAreaManager;
        private readonly ISettingsRepository _settingsRepo;
        private readonly IWindowsStartupManager _startupManager;
        private readonly ILogger _logger;
        private readonly IDaemonStatusProvider _daemonStatusProvider;

        public HealthUseCase(
            IScreenInfoProvider screenInfo,
            IWorkAreaManager workAreaManager,
            ISettingsRepository settingsRepo,
            IWindowsStartupManager startupManager,
            ILogger logger,
            IDaemonStatusProvider daemonStatusProvider)
        {
            _screenInfo = screenInfo ?? throw new ArgumentNullException(nameof(screenInfo));
            _workAreaManager = workAreaManager ?? throw new ArgumentNullException(nameof(workAreaManager));
            _settingsRepo = settingsRepo ?? throw new ArgumentNullException(nameof(settingsRepo));
            _startupManager = startupManager ?? throw new ArgumentNullException(nameof(startupManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _daemonStatusProvider = daemonStatusProvider ?? throw new ArgumentNullException(nameof(daemonStatusProvider));
        }

        /// <summary>
        /// Executes the health diagnostic and returns a complete HealthReport.
        /// </summary>
        public HealthReport Execute()
        {
            var report = new HealthReport();

            try
            {
                // Screen info
                report.ScreenWidth = _screenInfo.GetScreenWidth();
                report.ScreenHeight = _screenInfo.GetScreenHeight();

                // Work area status
                var currentStatus = _workAreaManager.GetStatus();
                if (currentStatus == null)
                {
                    report.Status = "Error Reading State";
                    report.ExitCode = 2;
                    return report;
                }

                report.CurrentWorkArea = currentStatus;

                // Settings
                var settings = _settingsRepo.Load();
                var desiredBottom = report.ScreenHeight - settings.ReservedBottomPixels;
                report.DesiredWorkArea = (0, 0, report.ScreenWidth, desiredBottom);
                report.Strategy = string.IsNullOrEmpty(settings.Strategy) ? "auto" : settings.Strategy;

                // Daemon detection
                report.DaemonRunning = _daemonStatusProvider.IsDaemonRunning();

                // Auto-start status
                report.AutoStartEnabled = _startupManager.IsInstalled();

                // Last reapply — currently N/A until shared state is implemented
                report.LastReapply = "N/A";

                // Determine overall status
                if (!report.DaemonRunning)
                {
                    report.Status = "Daemon Not Running";
                    report.ExitCode = 1;
                }
                else
                {
                    // Compare desired vs current work area
                    bool match = currentStatus.Value.left == 0 &&
                                 currentStatus.Value.top == 0 &&
                                 currentStatus.Value.right == report.ScreenWidth &&
                                 currentStatus.Value.bottom == desiredBottom;

                    report.Status = match ? "OK" : "Mismatch Detected";
                    report.ExitCode = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Health check failed: {ex.Message}");
                report.Status = "Error Reading State";
                report.ExitCode = 2;
            }

            return report;
        }
    }
}
