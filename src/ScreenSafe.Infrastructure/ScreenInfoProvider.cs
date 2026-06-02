using System;
using System.Runtime.InteropServices;
using ScreenSafe.Domain;

namespace ScreenSafe.Infrastructure
{
    /// <summary>
    /// Provides screen dimension information by calling GetSystemMetrics via P/Invoke.
    /// Avoids dependency on WinForms or WPF assemblies.
    /// </summary>
    public class ScreenInfoProvider : IScreenInfoProvider
    {
        /// <summary>
        /// Gets the full screen width in pixels by calling GetSystemMetrics(SM_CXSCREEN).
        /// SM_CXSCREEN = 0.
        /// </summary>
        public int GetScreenWidth()
        {
            return GetSystemMetrics(0);
        }

        /// <summary>
        /// Gets the full screen height in pixels by calling GetSystemMetrics(SM_CYSCREEN).
        /// SM_CYSCREEN = 1.
        /// </summary>
        public int GetScreenHeight()
        {
            return GetSystemMetrics(1);
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
    }
}
