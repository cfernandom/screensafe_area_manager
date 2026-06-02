using System;
using System.Runtime.InteropServices;

namespace ScreenSafe.Infrastructure.NativeMethods
{
    /// <summary>
    /// P/Invoke wrappers for shell32.dll appbar functions.
    /// </summary>
    public static class Shell32
    {
        // Appbar message types
        public const uint ABM_NEW = 0x0000;
        public const uint ABM_REMOVE = 0x0001;
        public const uint ABM_QUERYPOS = 0x0002;
        public const uint ABM_SETPOS = 0x0003;

        // Appbar edge values
        public const uint ABE_BOTTOM = 3;

        /// <summary>
        /// Sends an appbar message to the system.
        /// </summary>
        [DllImport("shell32.dll", SetLastError = true)]
        public static extern IntPtr SHAppBarMessage(uint msg, ref APPBARDATA pData);

        /// <summary>
        /// Finds a window by its class and window name.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowW(string lpClassName, string lpWindowName);
    }

    /// <summary>
    /// Represents the Win32 APPBARDATA structure for Shell32 appbar operations.
    /// Must use sequential layout for P/Invoke interop.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }
}
