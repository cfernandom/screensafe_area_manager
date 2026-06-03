using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure.NativeMethods;

namespace ScreenSafe.Infrastructure
{
    /// <summary>
    /// Monitors Win32 desktop events (work area changes, display changes,
    /// Explorer restart) and raises corresponding C# events.
    /// 
    /// Creates a hidden window via CreateWindowExW on a dedicated STA thread
    /// and runs a GetMessage message pump. Events received through the WndProc
    /// are forwarded to C# event handlers.
    /// 
    /// CRITICAL: The WndProc delegate is stored in a static readonly field to
    /// prevent garbage collection from collecting it after registration.
    /// </summary>
    public class WorkAreaWatcher : IWorkAreaWatcher
    {
        // ── Constants ───────────────────────────────────────────────────────

        private const string WindowClassName = "ScreenSafeHiddenWindow";

        // ── Static Fields ───────────────────────────────────────────────────

        /// <summary>
        /// Rooted WndProc delegate — prevents GC from collecting it while the
        /// window class is registered. This is a classic Win32 interop gotcha.
        /// </summary>
        private static readonly WndProcDelegate WndProcCallback = StaticWndProc;

        /// <summary>
        /// Maps HWND to WorkAreaWatcher instance for dispatch in the static WndProc.
        /// </summary>
        private static readonly ConcurrentDictionary<IntPtr, WorkAreaWatcher> Instances = new();

        /// <summary>
        /// Registered message ID for TaskbarCreated broadcast.
        /// </summary>
        private static uint _taskbarCreatedMessage;

        /// <summary>
        /// Tracks whether the window class has been registered process-wide.
        /// </summary>
        private static bool _classRegistered;

        /// <summary>
        /// Lock for static registration.
        /// </summary>
        private static readonly object _staticLock = new();

        // ── Delegate Type ───────────────────────────────────────────────────

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // ── Instance Fields ─────────────────────────────────────────────────

        private IntPtr _hwnd;
        private Thread? _pumpThread;
        private bool _running;

        // ── Events ──────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when the work area has been changed (WM_SETTINGCHANGE with SPI_SETWORKAREA).
        /// </summary>
        public event EventHandler? WorkAreaChanged;

        /// <summary>
        /// Raised when the display resolution or configuration changes (WM_DISPLAYCHANGE).
        /// </summary>
        public event EventHandler? DisplayChanged;

        /// <summary>
        /// Raised when Explorer has been restarted (TaskbarCreated message).
        /// </summary>
        public event EventHandler? ExplorerRestarted;

        // ── Properties ──────────────────────────────────────────────────────

        /// <summary>
        /// Handle to the hidden message-only window. IntPtr.Zero before Start().
        /// </summary>
        public IntPtr Hwnd => _hwnd;

        // ── Public Methods ──────────────────────────────────────────────────

        /// <summary>
        /// Starts the hidden window and Win32 message pump on a dedicated STA thread.
        /// Registers the window class and creates the hidden window.
        /// </summary>
        public void Start()
        {
            if (_running)
                return;

            _running = true;
            _pumpThread = new Thread(MessagePump)
            {
                Name = "ScreenSafe.WorkAreaWatcher",
                IsBackground = true
            };
            _pumpThread.SetApartmentState(ApartmentState.STA);
            _pumpThread.Start();

            // Wait for the window to be created
            var spinWait = new SpinWait();
            while (_hwnd == IntPtr.Zero && _pumpThread.IsAlive)
            {
                spinWait.SpinOnce();
            }
        }

        /// <summary>
        /// Stops the message pump and destroys the hidden window.
        /// </summary>
        public void Stop()
        {
            if (!_running)
                return;

            _running = false;

            if (_hwnd != IntPtr.Zero)
            {
                User32.DestroyWindow(_hwnd);
            }

            if (_pumpThread != null && _pumpThread.IsAlive)
            {
                if (!_pumpThread.Join(5000))
                {
                    // Force-abort if it doesn't stop gracefully
                    _pumpThread.Abort();
                }
            }

            _pumpThread = null;
        }

        // ── Message Pump ────────────────────────────────────────────────────

        /// <summary>
        /// Runs the message pump on a dedicated thread. Creates the hidden window,
        /// registers the window class, then loops on GetMessage until WM_QUIT.
        /// </summary>
        private void MessagePump()
        {
            lock (_staticLock)
            {
                if (!_classRegistered)
                {
                    RegisterWindowClass();
                    _classRegistered = true;
                }
            }

            _hwnd = CreateHiddenWindow();
            if (_hwnd == IntPtr.Zero)
            {
                _running = false;
                return;
            }

            Instances[_hwnd] = this;

            // Register the TaskbarCreated message
            _taskbarCreatedMessage = User32.RegisterWindowMessageW("TaskbarCreated");

            // Message pump
            while (User32.GetMessageW(out var msg, IntPtr.Zero, 0, 0))
            {
                User32.TranslateMessage(ref msg);
                User32.DispatchMessageW(ref msg);
            }

            // Cleanup after WM_QUIT
            Instances.TryRemove(_hwnd, out _);
            _hwnd = IntPtr.Zero;
        }

        // ── Window Creation ─────────────────────────────────────────────────

        private static void RegisterWindowClass()
        {
            var wndClass = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProcCallback),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = Marshal.GetHINSTANCE(typeof(WorkAreaWatcher).Module),
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = WindowClassName,
                hIconSm = IntPtr.Zero
            };

            var atom = User32.RegisterClassExW(ref wndClass);
            if (atom == 0)
            {
                throw new InvalidOperationException(
                    $"Failed to register window class. Error: {Marshal.GetLastWin32Error()}");
            }
        }

        private static IntPtr CreateHiddenWindow()
        {
            var hwnd = User32.CreateWindowExW(
                User32.WS_EX_TOOLWINDOW,
                WindowClassName,
                "ScreenSafe",           // Window title
                User32.WS_POPUP,        // No caption/overlapped styles
                0, 0, 0, 0,             // Position and size (irrelevant for hidden)
                IntPtr.Zero,            // No parent
                IntPtr.Zero,            // No menu
                Marshal.GetHINSTANCE(typeof(WorkAreaWatcher).Module),
                IntPtr.Zero);

            return hwnd;
        }

        // ── WndProc ─────────────────────────────────────────────────────────

        /// <summary>
        /// Static WndProc dispatched from Win32. Looks up the instance from the
        /// HWND and forwards to the instance handler.
        /// </summary>
        private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (Instances.TryGetValue(hWnd, out var instance))
            {
                return instance.InstanceWndProc(hWnd, msg, wParam, lParam);
            }

            return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        /// <summary>
        /// Instance-level WndProc that routes Win32 messages to C# events.
        /// </summary>
        private IntPtr InstanceWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == User32.WM_SETTINGCHANGE && (uint)wParam == User32.SPI_SETWORKAREA)
            {
                WorkAreaChanged?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (msg == User32.WM_DISPLAYCHANGE)
            {
                DisplayChanged?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage)
            {
                ExplorerRestarted?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (msg == User32.WM_DESTROY)
            {
                User32.PostQuitMessage(0);
                return IntPtr.Zero;
            }

            return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }
}
