using ScreenSafe.Domain;
using ScreenSafe.Infrastructure.NativeMethods;

namespace ScreenSafe.Infrastructure
{
    /// <summary>
    /// Implements IWorkAreaManager using User32's SystemParametersInfoW with SPI_SETWORKAREA.
    /// This is the primary strategy for managing the desktop work area.
    /// </summary>
    public class SpSetWorkAreaStrategy : IWorkAreaManager
    {
        private readonly IScreenInfoProvider _screenInfoProvider;

        /// <summary>
        /// Persists the work area in the user profile without broadcasting
        /// WM_SETTINGCHANGE. SPIF_SENDCHANGE is intentionally NOT included
        /// because some Windows installations (OEM drivers, display utilities)
        /// respond to the broadcast by reverting the work area, creating an
        /// infinite reapply loop.
        /// </summary>
        private const uint ApplyFlags = User32.SPIF_UPDATEINIFILE;

        public SpSetWorkAreaStrategy(IScreenInfoProvider screenInfoProvider)
        {
            _screenInfoProvider = screenInfoProvider ?? throw new ArgumentNullException(nameof(screenInfoProvider));
        }

        public bool Apply(int reservedBottomPixels)
        {
            var rect = default(RECT);

            if (!User32.SystemParametersInfoW(User32.SPI_GETWORKAREA, 0, ref rect, 0))
                return false;

            var screenHeight = _screenInfoProvider.GetScreenHeight();
            var newRect = CalculateNewWorkArea(rect, screenHeight, reservedBottomPixels);

            return User32.SystemParametersInfoW(
                User32.SPI_SETWORKAREA,
                0,
                ref newRect,
                ApplyFlags);
        }

        /// <summary>
        /// Restores the work area to the specified original bounds using SPI_SETWORKAREA.
        /// Stateless — the caller provides the pre-reservation work area from persistent storage.
        /// </summary>
        public bool Restore(ScreenRect originalArea)
        {
            var rect = new RECT
            {
                Left = originalArea.Left,
                Top = originalArea.Top,
                Right = originalArea.Right,
                Bottom = originalArea.Bottom
            };
            return User32.SystemParametersInfoW(
                User32.SPI_SETWORKAREA,
                0,
                ref rect,
                ApplyFlags);
        }

        public (int left, int top, int right, int bottom)? GetStatus()
        {
            var rect = default(RECT);

            if (!User32.SystemParametersInfoW(User32.SPI_GETWORKAREA, 0, ref rect, 0))
                return null;

            return (rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        /// <summary>
        /// Calculates a new work area RECT with the specified number of pixels
        /// reserved at the bottom, keeping left, top, and right unchanged.
        /// </summary>
        internal static RECT CalculateNewWorkArea(RECT originalRect, int screenHeight, int reservedBottomPixels)
        {
            return new RECT
            {
                Left = originalRect.Left,
                Top = originalRect.Top,
                Right = originalRect.Right,
                Bottom = screenHeight - reservedBottomPixels
            };
        }
    }
}
