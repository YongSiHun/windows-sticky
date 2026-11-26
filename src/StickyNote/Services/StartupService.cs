using System;
using Microsoft.Win32;

namespace StickyNote.Services
{
    public static class StartupService
    {
        private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        // MODE: apply requested display mode.
        public static void Apply(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(Key))
            {
                if (enabled) key.SetValue("StickyNote", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"", RegistryValueKind.String);
                else key.DeleteValue("StickyNote", false);
            }
        }
    }
}
