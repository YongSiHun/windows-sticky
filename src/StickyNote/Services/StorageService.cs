using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StickyNote.Models;
using StickyNote.Utilities;

namespace StickyNote.Services
{
    public sealed class StorageService : IDisposable
    {
        public string Root { get; private set; }
        private readonly LogService log;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private long savedRevision = -1;
        public bool RecoveryBlocked { get; private set; }
        public bool Recovered { get; private set; }
        public StorageService(string root, LogService log) { Root = root; this.log = log; Directory.CreateDirectory(root); }

        public static void AtomicWrite(string path, string text, string previousPath = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(text);
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, previousPath ?? path + ".bak", true);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private T Load<T>(string file, Func<T> empty, Action<T> validate)
        {
            string path = Path.Combine(Root, file);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return empty();
            try { var data = Json.Read<T>(File.ReadAllText(path)); validate(data); return data; }
            catch (NotSupportedException e) { log.Write("schema-" + file, e); RecoveryBlocked = true; return empty(); }
            catch (Exception e)
            {
                log.Write("load-" + file, e);
                try
                {
                    var backup = Json.Read<T>(File.ReadAllText(path + ".bak")); validate(backup);
                    // Archive corrupt data atomically without replacing the known-good .bak.
                    AtomicWrite(path, Json.Write(backup), path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                    Recovered = true; return backup;
                }
                catch (Exception failure) { log.Write("recovery-" + file, failure); RecoveryBlocked = true; return empty(); }
            }
        }
        public BackupDocument Load()
        {
            string journal = Path.Combine(Root, "restore.pending.json");
            if (File.Exists(journal))
            {
                try { var pending = ReadBackup(File.ReadAllText(journal)); WritePair(pending); File.Delete(journal); Recovered = true; }
                catch (Exception e) { log.Write("restore-replay", e); RecoveryBlocked = true; }
            }
            return new BackupDocument { notes = Load("notes.json", () => new NoteDocument(), Validation.Notes), settings = Load("settings.json", () => new Settings(), Validation.Settings) };
        }
        private void WritePair(BackupDocument data)
        {
            AtomicWrite(Path.Combine(Root, "notes.json"), Json.Write(data.notes));
            AtomicWrite(Path.Combine(Root, "settings.json"), Json.Write(data.settings));
        }
        public Task SaveAsync(BackupDocument snapshot, long revision)
        {
            return Task.Run(() =>
            {
                gate.Wait();
                try
                {
                    if (RecoveryBlocked) throw new InvalidOperationException("Storage is protected pending recovery.");
                    if (revision <= savedRevision) return;
                    Validation.Notes(snapshot.notes); Validation.Settings(snapshot.settings);
                    WritePair(snapshot); savedRevision = revision;
                }
                finally { gate.Release(); }
            });
        }
        public static BackupDocument ReadBackup(string json)
        {
            var data = Json.Read<BackupDocument>(json);
            Validation.Notes(data.notes); Validation.Settings(data.settings); return data;
        }
        public Task RestoreAsync(BackupDocument incoming, BackupDocument current, long revision)
        {
            Validation.Notes(incoming.notes); Validation.Settings(incoming.settings);
            return Task.Run(() =>
            {
                gate.Wait();
                try
                {
                    new BackupService(this).AutomaticBackup(current);
                    // Archive originals even when startup recovery was blocked.
                    foreach (var name in new[] { "notes.json", "settings.json", "restore.pending.json" })
                    {
                        var path = Path.Combine(Root, name);
                        if (File.Exists(path)) File.Copy(path, path + ".before-restore-" + Guid.NewGuid().ToString("N"));
                    }
                    var journal = Path.Combine(Root, "restore.pending.json");
                    AtomicWrite(journal, Json.Write(incoming));
                    RecoveryBlocked = true;
                    WritePair(incoming); File.Delete(journal);
                    savedRevision = revision; RecoveryBlocked = false;
                }
                finally { gate.Release(); }
            });
        }
        public void Dispose() { gate.Dispose(); }
    }
}
