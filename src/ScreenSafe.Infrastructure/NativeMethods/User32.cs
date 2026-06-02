using System;
using System.Runtime.InteropServices;

namespace ScreenSafe.Infrastructure.NativeMethods
{
    /// <summary>
    /// P/Invoke wrappers for user32.dll system parameters functions.
    /// </summary>
    public static class User32
    {
        public const uint SPI_GETWORKAREA = 0x0030;
        public const uint SPI_SETWORKAREA = 0x002F;

        public const uint SPIF_UPDATEINIFILE = 0x0001;
        public const uint SPIF_SENDCHANGE = 0x0002;
        public const uint SPIF_UPDATEINIFILE_AND_SENDCHANGE = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE;

        /// <summary>
        /// Retrieves or sets the current work area (desktop area excluding taskbar).
        /// dwAction: SPI_GETWORKAREA (0x0030) or SPI_SETWORKAREA (0x002F).
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfoW(
            uint action,
            uint uiParam,
            ref RECT pvParam,
            uint fWinIni);
    }

    /// <summary>
    /// Represents a Win32 RECT structure with left, top, right, bottom coordinates.
    /// Must use sequential layout for P/Invoke interop.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
