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
        {
            var sid = WindowsIdentity.GetCurrent().User;
            name = "StickyNote-" + sid.Value + "-" + System.Diagnostics.Process.GetCurrentProcess().SessionId;
            var security = new MutexSecurity(); security.AddAccessRule(new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow));
            bool created; mutex = new Mutex(true, @"Local\" + name, out created, security); IsFirst = created;
        }
        // IPC: bounded activation retries.
        public bool Signal()
        {
            for (int i = 0; i < 3; i++) try
            {
                // Connection itself is the activation signal; no payload/close race.
                using (var client = new NamedPipeClientStream(".", name, PipeDirection.Out)) { client.Connect(800); return true; }
            }
            catch (TimeoutException) { } catch (System.IO.IOException) { }
            return false;
        }
        // IPC: start connection listener.
        public void Listen(Action activate, LogService log)
        {
            Task.Run(() => ListenCore(activate, log));
        }
        // IPC: stop cleanly after disposal or transport failure.
        private void ListenCore(Action activate, LogService log)
        {
            try { while (Accept(activate)) { } }
            catch (Exception error) { ListenerFailed(error, log); }
        }
        // ACL: current user only.
        private NamedPipeServerStream CreateServer()
        {
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
            return new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
        }
        // IPC: connection is activation; no payload wait.
        private bool Accept(Action activate)
        {
            NamedPipeServerStream current;
            lock (sync)
            {
                if (disposed) return false;
                current = server = CreateServer();
            }
            current.WaitForConnection();
            if (current.IsConnected) activate();
            lock (sync) { current.Dispose(); if (server == current) server = null; }
            return true;
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
