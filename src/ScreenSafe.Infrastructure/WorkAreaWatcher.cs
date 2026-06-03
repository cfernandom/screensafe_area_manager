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
        /// Atom returned by RegisterClassExW. Used to create windows via atom
        /// lookup instead of string-based class name matching, avoiding
        /// ERROR_CANNOT_FIND_WND_CLASS (1407) on some Windows versions.
        /// </summary>
        private static ushort _classAtom;

        /// <summary>
        /// Lock for static registration.
        /// </summary>
        private static readonly object _staticLock = new();

        // ── Delegate Type ───────────────────────────────────────────────────

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // ── Instance Fields ─────────────────────────────────────────────────

        private readonly ILogger _logger;
        private IntPtr _hwnd;
        private Thread? _pumpThread;
        private bool _running;

        // ── Constructor ───────────────────────────────────────────────────

        public WorkAreaWatcher(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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
                    _logger.Info($"WorkAreaWatcher: window class '{WindowClassName}' registered");
                }
            }

            _hwnd = CreateHiddenWindow(_classAtom);
            if (_hwnd == IntPtr.Zero)
            {
                _logger.Error($"WorkAreaWatcher: CreateWindowExW failed. Error: {Marshal.GetLastWin32Error()}");
                _running = false;
                return;
            }

            _logger.Info($"WorkAreaWatcher: hidden window created (hwnd=0x{_hwnd.ToInt64():X})");

            Instances[_hwnd] = this;

            // Register the TaskbarCreated message
            _taskbarCreatedMessage = User32.RegisterWindowMessageW("TaskbarCreated");

            _logger.Info($"WorkAreaWatcher: message pump starting on thread {Thread.CurrentThread.ManagedThreadId}");

            // Message pump
            while (User32.GetMessageW(out var msg, IntPtr.Zero, 0, 0))
            {
                User32.TranslateMessage(ref msg);
                User32.DispatchMessageW(ref msg);
            }

            _logger.Info("WorkAreaWatcher: message pump stopped (WM_QUIT received)");

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
                hInstance = User32.GetModuleHandleW(null),
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
            _classAtom = atom;
        }

        private static IntPtr CreateHiddenWindow(ushort classAtom)
        {
            // Pass class atom via MAKEINTATOM equivalent instead of string class name.
            // On some Windows versions (especially 8.1 x64 with .NET Framework),
            // CreateWindowExW with string-based class name lookup fails with
            // ERROR_CANNOT_FIND_WND_CLASS (1407) even though RegisterClassExW
            // succeeded. Atom-based lookup bypasses string matching and module
            // resolution entirely.
            var atomPtr = new IntPtr((int)classAtom);

            // WS_OVERLAPPED (not WS_POPUP) is required to receive system
            // broadcasts like WM_SETTINGCHANGE. WS_POPUP windows are not
            // considered top-level for broadcasts on some Windows versions.
            var hwnd = User32.CreateWindowExW(
                User32.WS_EX_TOOLWINDOW,
                atomPtr,                // Class atom, not string
                "ScreenSafe",           // Window title
                User32.WS_OVERLAPPED,   // Overlapped (top-level, receives broadcasts)
                0, 0, 0, 0,             // Position and size (irrelevant for hidden)
                IntPtr.Zero,            // No parent
                IntPtr.Zero,            // No menu
                User32.GetModuleHandleW(null),
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
                _logger.Info($"WorkAreaWatcher: WM_SETTINGCHANGE/SPI_SETWORKAREA received");
                WorkAreaChanged?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (msg == User32.WM_DISPLAYCHANGE)
            {
                _logger.Info($"WorkAreaWatcher: WM_DISPLAYCHANGE received");
                DisplayChanged?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage)
            {
                _logger.Info($"WorkAreaWatcher: TaskbarCreated received");
                ExplorerRestarted?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (msg == User32.WM_DESTROY)
            {
                _logger.Info("WorkAreaWatcher: WM_DESTROY received, posting quit");
                User32.PostQuitMessage(0);
                return IntPtr.Zero;
            }

            return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }
}
