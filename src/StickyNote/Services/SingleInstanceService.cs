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
        public SingleInstanceService()
        {
            var sid = WindowsIdentity.GetCurrent().User;
            name = "StickyNote-" + sid.Value + "-" + System.Diagnostics.Process.GetCurrentProcess().SessionId;
            var security = new MutexSecurity(); security.AddAccessRule(new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow));
            bool created; mutex = new Mutex(true, @"Local\" + name, out created, security); IsFirst = created;
        }
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
        public void Listen(Action activate, LogService log)
        {
            Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        var security = new PipeSecurity();
                        security.SetAccessRuleProtection(true, false);
                        security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User, PipeAccessRights.FullControl, AccessControlType.Allow));
                        NamedPipeServerStream current;
                        lock (sync)
                        {
                            if (disposed) return;
                            current = server = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
                        }
                        current.WaitForConnection();
                        // Do not block waiting for a payload from a connected but silent client.
                        if (current.IsConnected) activate();
                        lock (sync) { current.Dispose(); if (server == current) server = null; }
                    }
                    catch (Exception e)
                    {
                        lock (sync) { if (disposed) return; if (server != null) server.Dispose(); server = null; }
                        log.Write("ipc-listener", e); return;
                    }
                }
            });
        }
        public void Dispose()
        {
            lock (sync) { disposed = true; if (server != null) server.Dispose(); }
            if (IsFirst) mutex.ReleaseMutex(); mutex.Dispose();
        }
    }
}
