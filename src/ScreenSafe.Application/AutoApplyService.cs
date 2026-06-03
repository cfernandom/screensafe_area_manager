using System;
using System.Collections.Concurrent;
using System.Threading;
using ScreenSafe.Domain;

namespace ScreenSafe.Application
{
    /// <summary>
    /// Orchestrates work area monitoring: subscribes to watcher events, debounces them,
    /// evaluates current vs desired work area, and reapplies when a mismatch is detected.
    /// Includes a circuit breaker that suspends automatic reapply after excessive attempts.
    /// </summary>
    public class AutoApplyService : IDisposable
    {
        private readonly IWorkAreaWatcher _watcher;
        private readonly IEventDebouncer _debouncer;
        private readonly IWorkAreaManager _workAreaManager;
        private readonly ISettingsRepository _settingsRepository;
        private readonly IScreenInfoProvider _screenInfoProvider;
        private readonly ILogger _logger;
        private readonly int _circuitBreakerMaxReapplies;
        private readonly int _circuitBreakerWindowSeconds;
        private readonly int _circuitBreakerSuspendSeconds;

        private readonly ConcurrentQueue<DateTime> _reapplyTimestamps = new();
        private readonly object _stateLock = new();
        private bool _running;
        private bool _suspended;
        private Timer? _suspendTimer;
        private bool _disposed;

        /// <summary>
        /// Creates a new AutoApplyService with the specified dependencies and circuit breaker configuration.
        /// </summary>
        /// <param name="watcher">Work area watcher for desktop events.</param>
        /// <param name="debouncer">Event debouncer to coalesce rapid events.</param>
        /// <param name="workAreaManager">Work area manager for SPI_GETWORKAREA/SPI_SETWORKAREA.</param>
        /// <param name="settingsRepository">Settings repository for reading configuration.</param>
        /// <param name="screenInfoProvider">Screen info provider for display dimensions.</param>
        /// <param name="logger">Logger for operational events.</param>
        /// <param name="circuitBreakerMaxReapplies">Max reapplies in the sliding window before suspension. Default 10.</param>
        /// <param name="circuitBreakerWindowSeconds">Sliding window in seconds for counting reapplies. Default 60.</param>
        /// <param name="circuitBreakerSuspendSeconds">Suspension duration in seconds. Default 300 (5 minutes).</param>
        public AutoApplyService(
            IWorkAreaWatcher watcher,
            IEventDebouncer debouncer,
            IWorkAreaManager workAreaManager,
            ISettingsRepository settingsRepository,
            IScreenInfoProvider screenInfoProvider,
            ILogger logger,
            int circuitBreakerMaxReapplies = 10,
            int circuitBreakerWindowSeconds = 60,
            int circuitBreakerSuspendSeconds = 300)
        {
            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
            _debouncer = debouncer ?? throw new ArgumentNullException(nameof(debouncer));
            _workAreaManager = workAreaManager ?? throw new ArgumentNullException(nameof(workAreaManager));
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _screenInfoProvider = screenInfoProvider ?? throw new ArgumentNullException(nameof(screenInfoProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _circuitBreakerMaxReapplies = circuitBreakerMaxReapplies;
            _circuitBreakerWindowSeconds = circuitBreakerWindowSeconds;
            _circuitBreakerSuspendSeconds = circuitBreakerSuspendSeconds;
        }

        /// <summary>
        /// Starts monitoring: subscribes to watcher events and starts the debouncer.
        /// </summary>
        public void Start()
        {
            lock (_stateLock)
            {
                if (_running)
                    return;

                _running = true;
                _suspended = false;
            }

            _watcher.WorkAreaChanged += OnWatcherEvent;
            _watcher.DisplayChanged += OnWatcherEvent;
            _watcher.ExplorerRestarted += OnWatcherEvent;

            _watcher.Start();
            _debouncer.Start();

            _logger.Info("AutoApplyService started");
        }

        /// <summary>
        /// Stops monitoring: unsubscribes from events and stops the debouncer.
        /// </summary>
        public void Stop()
        {
            lock (_stateLock)
            {
                if (!_running)
                    return;

                _running = false;
                _suspended = false;
            }

            _watcher.WorkAreaChanged -= OnWatcherEvent;
            _watcher.DisplayChanged -= OnWatcherEvent;
            _watcher.ExplorerRestarted -= OnWatcherEvent;

            _debouncer.Stop();
            _watcher.Stop();

            _suspendTimer?.Dispose();
            _suspendTimer = null;

            _logger.Info("AutoApplyService stopped");
        }

        /// <summary>
        /// Handles watcher events by forwarding them to the debouncer.
        /// </summary>
        private void OnWatcherEvent(object? sender, EventArgs e)
        {
            _logger.Info("Work area change event received, debouncing...");
            _debouncer.OnNext(Evaluate);
        }

        /// <summary>
        /// Evaluates current work area against desired configuration.
        /// Reapplies only if a mismatch is detected and the circuit breaker allows it.
        /// </summary>
        internal void Evaluate()
        {
            lock (_stateLock)
            {
                if (!_running)
                    return;

                if (_suspended)
                {
                    _logger.Info("Circuit breaker active — reapply suspended, skipping evaluation");
                    return;
                }
            }

            try
            {
                var settings = _settingsRepository.Load();
                var currentStatus = _workAreaManager.GetStatus();

                if (currentStatus == null)
                {
                    _logger.Warning("Cannot read current work area — skipping evaluation");
                    return;
                }

                var screenHeight = _screenInfoProvider.GetScreenHeight();
                var desiredBottom = screenHeight - settings.ReservedBottomPixels;

                // Desired area: full width, reserved at bottom
                var current = currentStatus.Value;
                bool match = current.left == 0 &&
                             current.top == 0 &&
                             current.right == _screenInfoProvider.GetScreenWidth() &&
                             current.bottom == desiredBottom;

                if (match)
                {
                    _logger.Info("Current and desired areas match, no reapply needed");
                    return;
                }

                // Mismatch detected — check circuit breaker before applying
                if (IsCircuitBreakerOpen())
                {
                    _logger.Warning($"Reapply skipped — circuit breaker open ({_circuitBreakerSuspendSeconds}s suspension)");
                    return;
                }

                _logger.Info($"Reapplying desired work area (current bottom={current.bottom}, desired bottom={desiredBottom})");

                // Apply the desired area (never touch OriginalWorkArea)
                _workAreaManager.Apply(settings.ReservedBottomPixels);

                RecordReapply();
                _logger.Info("Work area reapplied successfully");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during work area evaluation: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks whether the circuit breaker should open (too many reapplies in window).
        /// If so, starts the suspension timer.
        /// </summary>
        private bool IsCircuitBreakerOpen()
        {
            TrimOldTimestamps();

            if (_reapplyTimestamps.Count >= _circuitBreakerMaxReapplies)
            {
                lock (_stateLock)
                {
                    if (!_suspended)
                    {
                        _suspended = true;
                        _logger.Error($"Circuit breaker activated — {_circuitBreakerMaxReapplies} reapplies in {_circuitBreakerWindowSeconds}s. Suspending for {_circuitBreakerSuspendSeconds}s.");

                        _suspendTimer?.Dispose();
                        _suspendTimer = new Timer(ResumeAfterSuspension, null,
                            TimeSpan.FromSeconds(_circuitBreakerSuspendSeconds),
                            Timeout.InfiniteTimeSpan);
                    }
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes timestamps older than the circuit breaker window.
        /// </summary>
        private void TrimOldTimestamps()
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-_circuitBreakerWindowSeconds);

            while (_reapplyTimestamps.TryPeek(out var timestamp) && timestamp < cutoff)
            {
                _reapplyTimestamps.TryDequeue(out _);
            }
        }

        /// <summary>
        /// Records a reapply timestamp in the circuit breaker tracking queue.
        /// </summary>
        private void RecordReapply()
        {
            _reapplyTimestamps.Enqueue(DateTime.UtcNow);
        }

        /// <summary>
        /// Called by the suspension timer when the suspension period expires.
        /// Resumes normal operation.
        /// </summary>
        private void ResumeAfterSuspension(object? state)
        {
            lock (_stateLock)
            {
                _suspended = false;
                while (_reapplyTimestamps.TryDequeue(out _)) { }
            }

            _logger.Info("Circuit breaker suspension ended — resumed normal operation");
        }

        /// <summary>
        /// Disposes the service. Calls Stop() and releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases managed and unmanaged resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Stop();
                _suspendTimer?.Dispose();
            }

            _disposed = true;
        }
    }
}
