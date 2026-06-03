using System;
using System.Runtime.InteropServices;

namespace ScreenSafe.Infrastructure.NativeMethods
{
    /// <summary>
    /// P/Invoke wrappers for user32.dll and kernel32.dll system functions.
    /// </summary>
    public static class User32
    {
        // ── System Parameters Info ──────────────────────────────────────────

        public const uint SPI_GETWORKAREA = 0x0030;
        public const uint SPI_SETWORKAREA = 0x002F;

        public const uint SPIF_UPDATEINIFILE = 0x0001;
        public const uint SPIF_SENDCHANGE = 0x0002;
        public const uint SPIF_UPDATEINIFILE_AND_SENDCHANGE = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE;

        // ── Window Messages ─────────────────────────────────────────────────

        public const uint WM_SETTINGCHANGE = 0x001A;
        public const uint WM_DISPLAYCHANGE = 0x007E;
        public const uint WM_QUIT = 0x0012;
        public const uint WM_CLOSE = 0x0010;

        // ── Window Creation ─────────────────────────────────────────────────

        public const int CW_USEDEFAULT = unchecked((int)0x80000000);

        // ── SystemParametersInfoW ────────────────────────────────────────────

        /// <summary>
        /// Retrieves or sets a system parameter.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfoW(
            uint action,
            uint uiParam,
            ref RECT pvParam,
            uint fWinIni);

        // ── Window Creation and Message Pump ─────────────────────────────────

        /// <summary>
        /// Creates an extended window.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr CreateWindowExW(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        /// <summary>
        /// Default window procedure for messages not handled by the application.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr DefWindowProcW(
            IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Registers a window class for use in CreateWindowExW.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U2)]
        public static extern ushort RegisterClassExW(ref WNDCLASSEX lpwcx);

        /// <summary>
        /// Retrieves a message from the calling thread's message queue.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        /// <summary>
        /// Translates virtual-key messages into character messages.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        /// <summary>
        /// Dispatches a message to a window procedure.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DispatchMessageW(ref MSG lpMsg);

        /// <summary>
        /// Registers a system-wide message identifier for a string.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint RegisterWindowMessageW(string lpString);

        /// <summary>
        /// Posts a quit message to the calling thread's message queue.
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern void PostQuitMessage(int nExitCode);

        // ── Console ─────────────────────────────────────────────────────────

        /// <summary>
        /// Detaches the calling process from its console window.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeConsole();

        // ── Mutex (Daemon Detection) ────────────────────────────────────────

        /// <summary>
        /// Creates or opens a named mutex object.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateMutexW(IntPtr lpMutexAttributes, [MarshalAs(UnmanagedType.Bool)] bool bInitialOwner, string lpName);

        /// <summary>
        /// Opens an existing named mutex object.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenMutexW(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, string lpName);
    }

    // ── Win32 Structs ──────────────────────────────────────────────────────

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

    /// <summary>
    /// Represents a Win32 POINT structure with x, y coordinates.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    /// <summary>
    /// Represents the WNDCLASSEX window class structure.
    /// Must use sequential layout and Unicode encoding for P/Invoke interop.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    /// <summary>
    /// Represents a Win32 MSG message structure.
    /// Must use sequential layout for P/Invoke interop.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }
}
