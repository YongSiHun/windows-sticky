using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using StickyNote.Interop;

namespace StickyNote.Services
{
    // Remain a top-level popup: cross-process SetParent can break focus, DPI, and WPF lifetime.
    // No Shell owner either: Explorer destroying an owner can destroy its owned windows.
    public sealed class DesktopWidgetService : IDisposable
    {
        private readonly Window window;
        private readonly LogService log;
        private readonly HwndSource source;
        private readonly IntPtr handle;
        private IntPtr shell, iconHost;
        private readonly uint taskbarCreated;
        private readonly DispatcherTimer recovery;
        private readonly NativeMethods.WinEventProc foregroundCallback;
        private readonly IntPtr foregroundHook;
        private int retries;
        private bool ordering, disposed;
        private string _requestedMode = "normal";
        public string RequestedMode { get { return _requestedMode; } private set { _requestedMode = value; } }
        private string _actualMode = "normal";
        public string ActualMode { get { return _actualMode; } private set { _actualMode = value; } }
        public event EventHandler Changed;
        public DesktopWidgetService(Window window, LogService log)
        {
            this.window = window; this.log = log;
            handle = new WindowInteropHelper(window).Handle; source = HwndSource.FromHwnd(handle);
            taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated"); source.AddHook(Hook);
            recovery = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; recovery.Tick += Recover;
            foregroundCallback = ForegroundChanged;
            foregroundHook = NativeMethods.SetWinEventHook(3, 3, IntPtr.Zero, foregroundCallback, 0, 0, 0);
            window.Activated += Activated;
        }
        private bool FindShell()
        {
            shell = NativeMethods.GetShellWindow(); iconHost = IntPtr.Zero;
            uint shellProcess; NativeMethods.GetWindowThreadProcessId(shell, out shellProcess);
            NativeMethods.EnumWindows((candidate, unused) =>
            {
                if (NativeMethods.FindWindowEx(candidate, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                {
                    uint process; NativeMethods.GetWindowThreadProcessId(candidate, out process);
                    if (process == shellProcess) { iconHost = candidate; return false; }
                }
                return true;
            }, IntPtr.Zero);
            return NativeMethods.IsWindow(shell) && NativeMethods.IsWindow(iconHost);
        }
        private void Restore()
        {
            if (!NativeMethods.IsWindow(handle)) return;
            // No reparenting/owner/style mutations to undo. Preserve live WPF visibility,
            // layered-opacity and resize bits rather than replaying pre-Show styles.
            window.Topmost = false;
            if (!NativeMethods.SetWindowPos(handle, NativeMethods.NoTopmost, 0, 0, 0, 0, NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged)) throw new Win32Exception();
        }
        public void Apply(string requested)
        {
            RequestedMode = requested; recovery.Stop(); retries = 0;
            ApplyCore();
        }
        private void ApplyCore()
        {
            try
            {
                ActualMode = "normal"; Restore();
                if (RequestedMode == "topmost") { window.Topmost = true; ActualMode = "topmost"; }
                else if (RequestedMode == "desktop")
                {
                    if (!FindShell()) { log.Write("desktop-shell-unavailable"); throw new InvalidOperationException("Shell structure unavailable."); }
                    if (foregroundHook == IntPtr.Zero) throw new Win32Exception();
                    ActualMode = "desktop"; Order();
                }
            }
            catch (Exception e)
            {
                log.Write("desktop-fallback", e); ActualMode = "normal";
                try { Restore(); } catch (Exception restoreError) { log.Write("desktop-restore", restoreError); }
            }
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
        private void Order()
        {
            if (ordering || ActualMode != "desktop") return;
            ordering = true;
            try
            {
                // Change only our HWND, never the Shell's Z-order or parent.
                if (!NativeMethods.IsWindow(shell) || !NativeMethods.IsWindow(iconHost)) throw new InvalidOperationException();
                const uint flags = NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate;
                if (!NativeMethods.SetWindowPos(handle, NativeMethods.Bottom, 0, 0, 0, 0, flags)) throw new Win32Exception();
                IntPtr previous = NativeMethods.GetWindow(iconHost, 3); // GW_HWNDPREV; zero means HWND_TOP.
                if (!NativeMethods.SetWindowPos(handle, previous, 0, 0, 0, 0, flags)) throw new Win32Exception();
            }
            finally { ordering = false; }
        }
        private void Activated(object sender, EventArgs e)
        {
            if (ActualMode != "desktop") return;
            try { Order(); } catch (Exception error) { log.Write("desktop-order", error); ApplyCore(); }
        }
        private void ForegroundChanged(IntPtr hook, uint eventId, IntPtr foreground, int objectId, int childId, uint thread, uint time)
        {
            if (disposed) return;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (disposed || ActualMode != "desktop" || (foreground != shell && foreground != iconHost)) return;
                try
                {
                    // Best effort after Show Desktop; do not steal focus from desktop icons.
                    window.WindowState = WindowState.Normal;
                    NativeMethods.ShowWindow(handle, 4); Order();
                }
                catch (Exception error) { log.Write("desktop-foreground", error); ApplyCore(); }
            }));
        }
        private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == taskbarCreated && RequestedMode == "desktop")
            {
                // A bounded retry burst on Explorer's broadcast, no periodic polling.
                retries = 0; recovery.Stop(); recovery.Start();
            }
            if (message == NativeMethods.WmWindowPosChanging && ActualMode == "desktop" && !ordering)
            {
                if (!NativeMethods.IsWindow(shell) || !NativeMethods.IsWindow(iconHost))
                {
                    window.Dispatcher.BeginInvoke(new Action(() => { if (!disposed) ApplyCore(); }));
                }
                else
                {
                    var pos = (NativeMethods.WindowPos)Marshal.PtrToStructure(lParam, typeof(NativeMethods.WindowPos));
                    // Prevent activation from raising Desktop notes above applications; keep editable focus.
                    pos.flags |= 0x4; Marshal.StructureToPtr(pos, lParam, false);
                }
            }
            return IntPtr.Zero;
        }
        private void Recover(object sender, EventArgs e)
        {
            if (++retries >= 3) recovery.Stop();
            ApplyCore(); if (ActualMode == "desktop") recovery.Stop();
        }
        public void Dispose()
        {
            disposed = true; recovery.Stop(); recovery.Tick -= Recover;
            if (foregroundHook != IntPtr.Zero) NativeMethods.UnhookWinEvent(foregroundHook);
            window.Activated -= Activated; source.RemoveHook(Hook);
            ActualMode = "normal";
            try { Restore(); } catch (Exception e) { log.Write("desktop-dispose", e); }
        }
    }
}
