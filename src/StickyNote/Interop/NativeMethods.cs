using System;
using System.Runtime.InteropServices;

namespace StickyNote.Interop
{
    internal static class NativeMethods
    {
        internal const uint SwpNoSize = 1, SwpNoMove = 2, SwpNoActivate = 0x10, SwpFrameChanged = 0x20;
        internal const int WmWindowPosChanging = 0x46;
        internal static readonly IntPtr Bottom = new IntPtr(1), NoTopmost = new IntPtr(-2);
        // IPC: transfer a user-initiated launch's foreground permission to the existing process.
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint process);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool AllowSetForegroundWindow(uint process);
        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(IntPtr window);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(IntPtr window);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string message);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll")] internal static extern IntPtr GetShellWindow();
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window, uint command);
        // HWND: restore visibility without activation.
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
        internal delegate void WinEventProc(IntPtr hook, uint eventId, IntPtr window, int objectId, int childId, uint thread, uint time);
        // NATIVE: Win7-compatible entry point.
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWinEventHook(uint minimum, uint maximum, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
        // EVT: release foreground hook.
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [StructLayout(LayoutKind.Sequential)] internal struct WindowPos { public IntPtr hwnd, insertAfter; public int x, y, cx, cy; public uint flags; }
    }
}
