using System;
using System.Runtime.InteropServices;

namespace StickyNote.Interop
{
    internal static class NativeMethods
    {
        internal const int GwlStyle = -16, GwlExStyle = -20, GwlpOwner = -8;
        internal const long WsChild = 0x40000000L, WsPopup = 0x80000000L, WsExToolWindow = 0x80L;
        internal const uint SwpNoSize = 1, SwpNoMove = 2, SwpNoActivate = 0x10, SwpFrameChanged = 0x20;
        internal const int WmWindowPosChanging = 0x46, WmDisplayChange = 0x7E, WmSettingChange = 0x1A;
        internal static readonly IntPtr Bottom = new IntPtr(1), NoTopmost = new IntPtr(-2);
        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int Get32(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern IntPtr Get64(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int Set32(IntPtr window, int index, int value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern IntPtr Set64(IntPtr window, int index, IntPtr value);
        [DllImport("kernel32.dll")] internal static extern void SetLastError(uint error);
        internal static IntPtr GetLong(IntPtr window, int index) { return IntPtr.Size == 8 ? Get64(window, index) : new IntPtr(Get32(window, index)); }
        internal static void SetLong(IntPtr window, int index, IntPtr value)
        {
            SetLastError(0);
            var previous = IntPtr.Size == 8 ? Set64(window, index, value) : new IntPtr(Set32(window, index, value.ToInt32()));
            if (previous == IntPtr.Zero && Marshal.GetLastWin32Error() != 0) throw new System.ComponentModel.Win32Exception();
        }
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
        [DllImport("user32.dll")] internal static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        internal delegate void WinEventProc(IntPtr hook, uint eventId, IntPtr window, int objectId, int childId, uint thread, uint time);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWinEventHook(uint minimum, uint maximum, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(IntPtr window);
        [StructLayout(LayoutKind.Sequential)] internal struct WindowPos { public IntPtr hwnd, insertAfter; public int x, y, cx, cy; public uint flags; }
    }
}
