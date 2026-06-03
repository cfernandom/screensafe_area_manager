using System;
using System.Threading;
using ScreenSafe.Domain;

namespace ScreenSafe.Infrastructure
{
    /// <summary>
    /// Timer-based event debouncer. Each OnNext call resets the timer with the configured interval.
    /// The callback fires only after the interval elapses without new OnNext calls.
    /// Thread-safe: uses lock for state transitions.
    /// </summary>
    public class EventDebouncer : IEventDebouncer, IDisposable
    {
        private readonly int _intervalMs;
        private Timer? _timer;
        private Action? _callback;
        private readonly object _lock = new();
        private bool _disposed;

        /// <summary>
        /// Creates a new EventDebouncer with the specified interval.
        /// </summary>
        /// <param name="intervalMs">Debounce interval in milliseconds. Default 400ms.</param>
        public EventDebouncer(int intervalMs = 400)
        {
            if (intervalMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs),
                    "Interval must be greater than zero.");

            _intervalMs = intervalMs;
        }

        /// <summary>
        /// Starts the debounce timer. Must be called before OnNext.
        /// Creates the underlying Timer in a paused state.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                if (_timer == null)
                {
                    _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
                }
            }
        }

        /// <summary>
        /// Signals that an event occurred. Resets the debounce timer.
        /// The callback will fire after the configured interval with no new events.
        /// </summary>
        /// <param name="callback">Action to invoke when the debounce interval elapses.</param>
        public void OnNext(Action callback)
        {
            lock (_lock)
            {
                ThrowIfDisposed();

                _callback = callback ?? throw new ArgumentNullException(nameof(callback));
                _timer?.Change(_intervalMs, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Stops the debounce timer and releases resources.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
                _callback = null;
            }
        }

        /// <summary>
        /// Releases all resources used by the EventDebouncer.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _timer?.Dispose();
                    _timer = null;
                    _callback = null;
                }
            }
        }

        private void TimerCallback(object? state)
        {
            Action? callback;
            lock (_lock)
            {
                callback = _callback;
                _callback = null;
            }

            callback?.Invoke();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EventDebouncer));
        }
    }
}
