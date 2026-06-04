using System;

namespace ScreenSafe.Domain
{
    /// <summary>
    /// Debounces rapid event sequences into a single callback invocation.
    /// Timer-based with configurable interval and single-fire restart pattern.
    /// </summary>
    public interface IEventDebouncer
    {
        /// <summary>
        /// Signals that an event occurred. Resets the debounce timer.
        /// The callback will fire after the configured interval with no new events.
        /// </summary>
        /// <param name="callback">Action to invoke when the debounce interval elapses.</param>
        void OnNext(Action callback);

        /// <summary>
        /// Starts the debounce timer. Must be called before OnNext.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the debounce timer and disposes resources.
        /// </summary>
        void Stop();
    }
}
