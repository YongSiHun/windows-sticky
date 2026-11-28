using System;
using System.Runtime.InteropServices;

namespace StickyNote.Interop
{
    internal static class NativeMethods
    {
        internal const uint SwpNoSize = 1, SwpNoMove = 2, SwpNoZOrder = 4, SwpNoActivate = 0x10, SwpFrameChanged = 0x20;
        internal const int WmWindowPosChanging = 0x46;
        internal const uint EventSystemForeground = 3, GwHwndPrevious = 3;
        internal const int SwShowNoActivate = 4;
        internal static readonly IntPtr Bottom = new IntPtr(1), NoTopmost = new IntPtr(-2);
        // Identify the instance that owns the connected pipe before transferring focus permission.
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint process);

        // Transfer a user-initiated launch's foreground permission to the existing process.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AllowSetForegroundWindow(uint process);

        // Return false to stop enumerating after the desktop icon host is found.
        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        // Locate a child by class name without changing Shell ownership or parentage.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);

        // Enumerate top-level windows to discover Explorer's desktop icon host.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        // Check handle lifetime before ordering or restoring a window.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        // Inspect native visibility in regression tests after mode transitions.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        // Inspect native parentage in regression tests to detect accidental reparenting.
        [DllImport("user32.dll")]
        internal static extern IntPtr GetParent(IntPtr window);

        // Reorder only the note window while preserving bounds and focus through flags.
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);

        // Verify the discovered icon host belongs to the Shell process.
        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        // Obtain the shared TaskbarCreated message used to retry Shell integration.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string message);

        // Find the desktop Shell window without creating or modifying it.
        [DllImport("user32.dll")]
        internal static extern IntPtr GetShellWindow();

        // Find the window before the icon host in native stacking order.
        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr window, uint command);

        // Restore note visibility without activating it when using SwShowNoActivate.
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr window, int command);

        // Receive foreground changes; retain this delegate until the hook is released.
        internal delegate void WinEventProc(IntPtr hook, uint eventId, IntPtr window, int objectId, int childId, uint thread, uint time);

        // Observe desktop foreground changes using a Windows 7-compatible hook.
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWinEventHook(uint minimum, uint maximum, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);

        // Release the foreground hook before its managed callback can be collected.
        [DllImport("user32.dll")]
        internal static extern bool UnhookWinEvent(IntPtr hook);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowPos
        {
            public IntPtr hwnd, insertAfter;
            public int x, y, cx, cy;
            public uint flags;
        }
    }
}
