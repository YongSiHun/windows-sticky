using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using StickyNote.Models;

namespace StickyNote.Services
{
    // All public calls are dispatcher-affine. Background storage receives detached snapshots only.
    public sealed class AutosaveService : IDisposable
    {
        private readonly DispatcherTimer timer;
        private readonly Func<BackupDocument> snapshot;
        private readonly StorageService storage;
        private readonly Action<Exception> failed;
        private long revision;
        private bool dirty;
        private Task pending = Task.FromResult(0);
        public AutosaveService(StorageService storage, Func<BackupDocument> snapshot, Action<Exception> failed, int delay = 500)
        {
            this.storage = storage; this.snapshot = snapshot; this.failed = failed;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) }; timer.Tick += Tick;
        }
        public void Request() { dirty = true; revision++; timer.Stop(); timer.Start(); }
        private async void Tick(object sender, EventArgs e)
        {
            timer.Stop();
            try { await FlushAsync(); } catch (Exception error) { failed(error); }
        }
        public Task FlushAsync()
        {
            timer.Stop();
            if (!dirty) return pending;
            var captured = revision; var data = snapshot(); dirty = false;
            pending = Save(data, captured); return pending;
        }
        private async Task Save(BackupDocument data, long captured)
        {
            try { await storage.SaveAsync(data, captured); }
            catch { dirty = true; throw; }
        }
        public void ResetAfterRestore() { revision++; dirty = false; timer.Stop(); }
        public long NextRevision() { return ++revision; }
        public void Dispose() { timer.Stop(); timer.Tick -= Tick; }
    }
}
