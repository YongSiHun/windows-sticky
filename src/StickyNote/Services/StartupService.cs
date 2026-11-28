using System;
using Microsoft.Win32;

namespace StickyNote.Services
{
    public static class StartupService
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string InstallKey = @"Software\StickyNote";
        // SETUP: installer uses a fixed 32-bit view for either package architecture.
        internal static bool? InstallationChoice()
        {
            using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
            using (var key = user.OpenSubKey(InstallKey))
            {
                var value = key == null ? null : key.GetValue("InstallStartupEnabled");
                if (value is int && ((int)value == 0 || (int)value == 1)) return (int)value == 1;
                return null;
            }
        }

        // Remove the one-time installer choice after settings are durably saved.
        internal static void ClearInstallationChoice()
        {
            using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
            using (var key = user.OpenSubKey(InstallKey, true))
            {
                if (key != null) key.DeleteValue("InstallStartupEnabled", false);
            }
        }

        internal static string Command
        {
            get
            {
                return "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"";
            }
        }

        // Verify the Run entry matches this executable, including path quoting.
        public static bool IsRegistered()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(Key))
            {
                return key != null && string.Equals(key.GetValue("StickyNote") as string, Command, StringComparison.OrdinalIgnoreCase);
            }
        }

        // START: register the requested login behavior.
        public static void Apply(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(Key))
            {
                if (enabled) key.SetValue("StickyNote", Command, RegistryValueKind.String);
                else key.DeleteValue("StickyNote", false);
            }
            if (enabled && !IsRegistered()) throw new InvalidOperationException("Startup registration verification failed.");
        }
    }
}
