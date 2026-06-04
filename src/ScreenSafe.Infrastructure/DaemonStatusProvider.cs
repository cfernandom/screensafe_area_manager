using System;
using System.Runtime.InteropServices;
using ScreenSafe.Domain;
using ScreenSafe.Infrastructure.NativeMethods;

namespace ScreenSafe.Infrastructure
{
    /// <summary>
    /// Detects whether the ScreenSafe daemon is running by checking for the
    /// named mutex "Global\ScreenSafeDaemon" using Win32 OpenMutexW.
    /// </summary>
    public class DaemonStatusProvider : IDaemonStatusProvider
    {
        /// <summary>
        /// Maximum access rights needed to open a mutex for querying.
        /// </summary>
        private const uint MUTEX_ALL_ACCESS = 0x1F0001;

        /// <summary>
        /// Win32 error code for "file not found" (mutex does not exist).
        /// </summary>
        private const int ERROR_FILE_NOT_FOUND = 2;

        /// <summary>
        /// Checks if the ScreenSafe daemon mutex exists.
        /// </summary>
        public bool IsDaemonRunning()
        {
            IntPtr handle = User32.OpenMutexW(MUTEX_ALL_ACCESS, false, "Global\\ScreenSafeDaemon");
            if (handle != IntPtr.Zero)
            {
                User32.CloseHandle(handle);
                return true;
            }

            return Marshal.GetLastWin32Error() != ERROR_FILE_NOT_FOUND;
        }
    }
}
