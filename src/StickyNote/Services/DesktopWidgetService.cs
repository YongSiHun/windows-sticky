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
        public string RequestedMode
        {
            get
            {
                return _requestedMode;
            }
            private set
            {
                _requestedMode = value;
            }
        }

        private string _actualMode = "normal";
        public string ActualMode
        {
            get
            {
                return _actualMode;
            }
            private set
            {
                _actualMode = value;
            }
        }

        // Observe native ordering and Shell recovery for this note window.
        public DesktopWidgetService(Window window, LogService log)
        {
            this.window = window;
            this.log = log;
            handle = new WindowInteropHelper(window).Handle;
            source = HwndSource.FromHwnd(handle);
            taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            source.AddHook(Hook);
            recovery = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            recovery.Tick += Recover;
            foregroundCallback = ForegroundChanged;
            foregroundHook = NativeMethods.SetWinEventHook(NativeMethods.EventSystemForeground, NativeMethods.EventSystemForeground,
                IntPtr.Zero, foregroundCallback, 0, 0, 0);
            window.Activated += Activated;
        }

        // SHELL: locate live desktop host.
        private bool FindShell()
        {
            shell = NativeMethods.GetShellWindow();
            iconHost = IntPtr.Zero;
            uint shellProcess;
            NativeMethods.GetWindowThreadProcessId(shell, out shellProcess);
            NativeMethods.EnumWindows((candidate, unused) => FindIconHost(candidate, shellProcess), IntPtr.Zero);
            return NativeMethods.IsWindow(shell) && NativeMethods.IsWindow(iconHost);
        }

        // HWND: accept only Explorer's icon host.
        private bool FindIconHost(IntPtr candidate, uint shellProcess)
        {
            if (NativeMethods.FindWindowEx(candidate, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero) return true;
            uint process;
            NativeMethods.GetWindowThreadProcessId(candidate, out process);
            if (process != shellProcess) return true;
            iconHost = candidate;
            return false;
        }

        // MODE: restore usable Normal mode.
        private void Restore()
        {
            if (!NativeMethods.IsWindow(handle)) return;
            // No reparenting/owner/style mutations to undo. Preserve live WPF visibility,
            // layered-opacity and resize bits rather than replaying pre-Show styles.
            window.Topmost = false;
            if (!NativeMethods.SetWindowPos(handle, NativeMethods.NoTopmost, 0, 0, 0, 0, NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged)) throw new Win32Exception();
        }

        // MODE: apply requested display mode.
        public void Apply(string requested)
        {
            RequestedMode = requested;
            recovery.Stop();
            retries = 0;
            ApplyCore();
        }

        // MODE: preserve request through fallback.
        private void ApplyCore()
        {
            try
            {
                ActualMode = "normal";
                Restore();
                ApplyRequestedMode();
            }
            catch (Exception e)
            {
                log.Write("desktop-fallback", e);
                ActualMode = "normal";
                try
                {
                    Restore();
                }
                catch (Exception restoreError)
                {
                    log.Write("desktop-restore", restoreError);
                }
            }
        }

        // MODE: apply request; caller owns fallback.
        private void ApplyRequestedMode()
        {
            if (RequestedMode == "topmost")
            {
                window.Topmost = true;
                ActualMode = "topmost";
                return;
            }
            if (RequestedMode != "desktop") return;
            if (!FindShell()) throw new InvalidOperationException("Shell structure unavailable.");
            if (foregroundHook == IntPtr.Zero) throw new Win32Exception();
            ActualMode = "desktop";
            Order();
        }

        // Z: maintain desktop stacking.
        private void Order()
        {
            if (ordering || ActualMode != "desktop") return;
            ordering = true;
            try
            {
                // Change only our HWND, never the Shell's Z-order or parent.
                if (!NativeMethods.IsWindow(shell) || !NativeMethods.IsWindow(iconHost)) throw new InvalidOperationException();
                MoveAfter(NativeMethods.Bottom);
                IntPtr previous = NativeMethods.GetWindow(iconHost, NativeMethods.GwHwndPrevious);
                // A zero previous handle means HWND_TOP.
                MoveAfter(previous);
            }
            finally
            {
                ordering = false;
            }
        }

        // Z: change only our HWND, preserving bounds/focus.
        private void MoveAfter(IntPtr after)
        {
            const uint flags = NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate;
            if (!NativeMethods.SetWindowPos(handle, after, 0, 0, 0, 0, flags)) throw new Win32Exception();
        }

        // Z: restore desktop order after activation.
        private void Activated(object sender, EventArgs e)
        {
            if (ActualMode != "desktop") return;
            try
            {
                Order();
            }
            catch (Exception error)
            {
                log.Write("desktop-order", error);
                ApplyCore();
            }
        }

        // EVT: queue only relevant desktop activation.
        private void ForegroundChanged(IntPtr hook, uint eventId, IntPtr foreground, int objectId, int childId, uint thread, uint time)
        {
            if (disposed) return;
            if (ActualMode != "desktop") return;
            if (foreground != shell && foreground != iconHost) return;
            window.Dispatcher.BeginInvoke(new Action(RestoreDesktopVisibility));
        }

        // UI: best effort after Show Desktop without focus theft.
        private void RestoreDesktopVisibility()
        {
            if (disposed || ActualMode != "desktop") return;
            try
            {
                window.WindowState = WindowState.Normal;
                NativeMethods.ShowWindow(handle, NativeMethods.SwShowNoActivate);
                Order();
            }
            catch (Exception error)
            {
                log.Write("desktop-foreground", error);
                ApplyCore();
            }
        }

        // MSG: handle Shell recovery and window ordering.
        private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == taskbarCreated && RequestedMode == "desktop")
            {
                // A bounded retry burst on Explorer's broadcast, no periodic polling.
                retries = 0;
                recovery.Stop();
                recovery.Start();
            }
            if (message == NativeMethods.WmWindowPosChanging) PreserveDesktopOrder(lParam);
            return IntPtr.Zero;
        }

        // Z: block activation from raising Desktop notes over apps.
        private void PreserveDesktopOrder(IntPtr lParam)
        {
            if (ActualMode != "desktop" || ordering) return;
            if (!NativeMethods.IsWindow(shell) || !NativeMethods.IsWindow(iconHost))
            {
                window.Dispatcher.BeginInvoke(new Action(ApplyIfAlive));
                return;
            }
            var pos = (NativeMethods.WindowPos)Marshal.PtrToStructure(lParam, typeof(NativeMethods.WindowPos));
            pos.flags |= NativeMethods.SwpNoZOrder;
            Marshal.StructureToPtr(pos, lParam, false);
        }

        // LIFE: queued callbacks may outlive HWND.
        private void ApplyIfAlive()
        {
            if (!disposed) ApplyCore();
        }

        // REC: bounded Shell recovery burst.
        private void Recover(object sender, EventArgs e)
        {
            if (++retries >= 3) recovery.Stop();
            ApplyCore();
            if (ActualMode == "desktop") recovery.Stop();
        }

        // FREE: release owned resources.
        public void Dispose()
        {
            disposed = true;
            recovery.Stop();
            recovery.Tick -= Recover;
            if (foregroundHook != IntPtr.Zero) NativeMethods.UnhookWinEvent(foregroundHook);
            window.Activated -= Activated;
            source.RemoveHook(Hook);
            ActualMode = "normal";
            try
            {
                Restore();
            }
            catch (Exception e)
            {
                log.Write("desktop-dispose", e);
            }
        }
    }
}
