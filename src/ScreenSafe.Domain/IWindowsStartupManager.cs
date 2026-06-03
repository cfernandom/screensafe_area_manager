namespace ScreenSafe.Domain
{
    /// <summary>
    /// Manages Windows auto-start registration via the HKCU\...\Run registry key.
    /// </summary>
    public interface IWindowsStartupManager
    {
        /// <summary>
        /// Installs the ScreenSafe daemon for automatic startup with Windows.
        /// </summary>
        void Install();

        /// <summary>
        /// Removes the ScreenSafe daemon from Windows automatic startup.
        /// </summary>
        void Uninstall();

        /// <summary>
        /// Checks whether ScreenSafe is registered for automatic startup.
        /// </summary>
        bool IsInstalled();

        /// <summary>
        /// Gets the registered command string, or null if not installed.
        /// </summary>
        string GetRegisteredCommand();
    }
}
