namespace ScreenSafe.Domain
{
    /// <summary>
    /// Defines operations to manage the Windows desktop work area.
    /// Implementations use platform-specific P/Invoke strategies.
    /// </summary>
    public interface IWorkAreaManager
    {
        /// <summary>
        /// Reserves the specified number of pixels at the bottom of the screen.
        /// Stores the original work area for later restoration.
        /// </summary>
        /// <param name="reservedBottomPixels">Number of pixels to reserve at the bottom.</param>
        /// <returns>True if the operation succeeded.</returns>
        bool Apply(int reservedBottomPixels);

        /// <summary>
        /// Restores the original full-screen work area.
        /// </summary>
        /// <returns>True if the operation succeeded.</returns>
        bool Restore();

        /// <summary>
        /// Gets the current work area bounds.
        /// </summary>
        /// <returns>A tuple of (left, top, right, bottom), or null on failure.</returns>
        (int left, int top, int right, int bottom)? GetStatus();
    }
}
