using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace StickyNote.Services
{
    public sealed class SingleInstanceService : IDisposable
    {
        private readonly Mutex mutex;
        private readonly string name;
        private readonly object sync = new object();
        private NamedPipeServerStream server;
        private bool disposed;
        public bool IsFirst { get; private set; }
        // INIT: wire owned state and events.
        public SingleInstanceService()
            : this(null)
        {
        }
        // QA: isolate test mutexes and pipes from the running application.
        internal SingleInstanceService(string scope)
        {
            var sid = WindowsIdentity.GetCurrent().User;
            name = "StickyNote-" + sid.Value + "-" + System.Diagnostics.Process.GetCurrentProcess().SessionId;
            if (scope != null) name += "-" + scope;
            var security = new MutexSecurity(); security.AddAccessRule(new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow));
            bool created; mutex = new Mutex(true, @"Local\" + name, out created, security); IsFirst = created;
        }
        // IPC: bounded activation retries.
        public bool Signal()
        {
            for (int i = 0; i < 3; i++)
            {
                if (TryActivate()) return true;
            }
            return false;
        }
        // UNINSTALL: request normal shutdown and wait until the executable is released.
        public bool RequestExit()
        {
            if (IsFirst) return true;
            try
            {
                uint process;
                if (!SendSignal(2, out process)) return false;
                return WaitForExit(process);
            }
            catch (TimeoutException) { return false; }
            catch (System.IO.IOException) { return false; }
        }
        private static bool WaitForExit(uint process)
        {
            try
            {
                using (var running = System.Diagnostics.Process.GetProcessById((int)process))
                    return running.WaitForExit(30000);
            }
            catch (ArgumentException) { return true; } // Already exited after the acknowledgment.
        }
        // IPC: treat transient transport failures as retryable.
        private bool TryActivate()
        {
            try { return ConnectAndWaitForActivation(); }
            catch (TimeoutException) { return false; }
            catch (System.IO.IOException) { return false; }
        }
        // IPC: keep the connection alive until activation is acknowledged.
        private bool ConnectAndWaitForActivation()
        {
            uint process;
            return SendSignal(1, out process);
        }
        private bool SendSignal(byte command, out uint process)
        {
            using (var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                client.Connect(800);
                if (!StickyNote.Interop.NativeMethods.GetNamedPipeServerProcessId(client.SafePipeHandle, out process)) return false;
                StickyNote.Interop.NativeMethods.AllowSetForegroundWindow(process);
                client.WriteByte(command);
                return ReadSignal(client);
            }
        }
        // IPC: both sides use a bounded handshake; an idle client cannot hold the listener.
        private static bool ReadSignal(System.IO.Stream stream)
        {
            return ReadCommand(stream) == 1;
        }
        private static int ReadCommand(System.IO.Stream stream)
        {
            var reply = new byte[1];
            var read = stream.ReadAsync(reply, 0, reply.Length);
            if (Task.WhenAny(read, Task.Delay(800)).GetAwaiter().GetResult() != read)
            {
                read.ContinueWith(task => task.Exception.Handle(error => true),
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                return -1;
            }
            return read.GetAwaiter().GetResult() == 1 ? reply[0] : -1;
        }
        // IPC: start connection listener.
        public void Listen(Action activate, LogService log, Action exit = null)
        {
            Task.Run(() => ListenCore(activate, exit, log));
        }
        // IPC: stop cleanly after disposal or transport failure.
        private void ListenCore(Action activate, Action exit, LogService log)
        {
            try { while (Accept(activate, exit)) { } }
            catch (Exception error) { ListenerFailed(error, log); }
        }
        // ACL: current user only.
        private NamedPipeServerStream CreateServer()
        {
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
            return new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
        }
        // IPC: acknowledge only after the activation callback has run.
        private bool Accept(Action activate, Action exit)
        {
            NamedPipeServerStream current;
            lock (sync)
            {
                if (disposed) return false;
                current = server = CreateServer();
            }
            try
            {
                current.WaitForConnection();
                DispatchCommand(current, activate, exit);
            }
            catch (System.IO.IOException) { /* A timed-out client must not stop the listener. */ }
            finally
            {
                lock (sync) { current.Dispose(); if (server == current) server = null; }
            }
            return true;
        }
        private static void DispatchCommand(System.IO.Stream current, Action activate, Action exit)
        {
            var command = ReadCommand(current);
            if (command == 2 && exit != null)
            {
                // Acknowledge before shutdown can dispose the pipe.
                current.WriteByte(1);
                exit();
                return;
            }
            if (command != 1) return;
            activate();
            current.WriteByte(1);
        }
        // ERR: ignore expected shutdown interruption.
        private void ListenerFailed(Exception error, LogService log)
        {
            lock (sync)
            {
                if (disposed) return;
                if (server != null) server.Dispose();
                server = null;
            }
            log.Write("ipc-listener", error);
        }
        // FREE: release owned resources.
        public void Dispose()
        {
            lock (sync) { disposed = true; if (server != null) server.Dispose(); }
            if (IsFirst) mutex.ReleaseMutex(); mutex.Dispose();
        }
    }
}
