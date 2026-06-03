using ScreenSafe.Domain;
using ScreenSafe.Infrastructure.NativeMethods;

namespace ScreenSafe.Infrastructure
{
    /// <summary>
    /// Implements IWorkAreaManager using Shell32's SHAppBarMessage with ABM_SETPOS
    /// as a fallback strategy for Windows 10+.
    /// </summary>
    public class ShAppBarMessageStrategy : IWorkAreaManager
    {
        private readonly IScreenInfoProvider _screenInfoProvider;

        public ShAppBarMessageStrategy(IScreenInfoProvider screenInfoProvider)
        {
            _screenInfoProvider = screenInfoProvider ?? throw new ArgumentNullException(nameof(screenInfoProvider));
        }

        public bool Apply(int reservedBottomPixels)
        {
            var hWnd = Shell32.FindWindowW("Progman", null!);
            if (hWnd == IntPtr.Zero)
                hWnd = Shell32.FindWindowW("Shell_TrayWnd", null!);

            var data = new APPBARDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(APPBARDATA)),
                hWnd = hWnd,
                uEdge = Shell32.ABE_BOTTOM
            };

            if (Shell32.SHAppBarMessage(Shell32.ABM_QUERYPOS, ref data) == IntPtr.Zero)
                return false;

            var screenHeight = _screenInfoProvider.GetScreenHeight();
            data.rc.Bottom = screenHeight - reservedBottomPixels;

            return Shell32.SHAppBarMessage(Shell32.ABM_SETPOS, ref data) != IntPtr.Zero;
        }

        /// <summary>
        /// Restores the appbar position to the specified original bounds.
        /// Stateless — the caller provides the pre-reservation area from persistent storage.
        /// </summary>
        public bool Restore(ScreenRect originalArea)
        {
            var hWnd = Shell32.FindWindowW("Progman", null!);
            if (hWnd == IntPtr.Zero)
                hWnd = Shell32.FindWindowW("Shell_TrayWnd", null!);

            var data = new APPBARDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(APPBARDATA)),
                hWnd = hWnd,
                uEdge = Shell32.ABE_BOTTOM
            };

            Shell32.SHAppBarMessage(Shell32.ABM_QUERYPOS, ref data);
            data.rc.Left = originalArea.Left;
            data.rc.Top = originalArea.Top;
            data.rc.Right = originalArea.Right;
            data.rc.Bottom = originalArea.Bottom;

            return Shell32.SHAppBarMessage(Shell32.ABM_SETPOS, ref data) != IntPtr.Zero;
        }

        public (int left, int top, int right, int bottom)? GetStatus()
        {
            var data = new APPBARDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(APPBARDATA))
            };

            if (Shell32.SHAppBarMessage(Shell32.ABM_QUERYPOS, ref data) == IntPtr.Zero)
                return null;

            return (data.rc.Left, data.rc.Top, data.rc.Right, data.rc.Bottom);
        }
    }
}
