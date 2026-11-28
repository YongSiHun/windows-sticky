using System;
using System.IO;
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
        // Prepare the data directory without reading or replacing existing documents.
        public StorageService(string root, LogService log)
        {
            Root = root;
            this.log = log;
            Directory.CreateDirectory(root);
        }

        // LOAD: validate data before use.
        private T Load<T>(string file, Func<T> empty, Action<T> validate)
        {
            string path = Path.Combine(Root, file);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return empty();
            try
            {
                var data = Json.Read<T>(File.ReadAllText(path));
                validate(data);
                return data;
            }
            catch (NotSupportedException e)
            {
                log.Write("schema-" + file, e);
                RecoveryBlocked = true;
                return empty();
            }
            catch (Exception e)
            {
                log.Write("load-" + file, e);
                return Recover(path, file, empty, validate);
            }
        }

        // BAK: preserve corrupt original and known-good backup.
        private T Recover<T>(string path, string file, Func<T> empty, Action<T> validate)
        {
            try
            {
                var backup = Json.Read<T>(File.ReadAllText(path + ".bak"));
                validate(backup);
                AtomicFile.Write(path, Json.Write(backup), path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                Recovered = true;
                return backup;
            }
            catch (Exception failure)
            {
                log.Write("recovery-" + file, failure);
                RecoveryBlocked = true;
                return empty();
            }
        }

        // LOAD: validate data before use.
        public BackupDocument Load()
        {
            ReplayPendingRestore();
            return new BackupDocument
            {
                notes = Load("notes.json", () => new NoteDocument(), Validation.Notes),
                settings = Load("settings.json", () => new Settings(), Validation.Settings)
            };
        }

        // Finish a journaled restore before reading either live document.
        private void ReplayPendingRestore()
        {
            string journal = Path.Combine(Root, "restore.pending.json");
            if (!File.Exists(journal)) return;
            try
            {
                var pending = ReadBackup(File.ReadAllText(journal));
                WritePair(pending);
                File.Delete(journal);
                Recovered = true;
            }
            catch (Exception e)
            {
                log.Write("restore-replay", e);
                RecoveryBlocked = true;
            }
        }

        // IO: replace validated notes and settings.
        private void WritePair(BackupDocument data)
        {
            AtomicFile.Write(Path.Combine(Root, "notes.json"), Json.Write(data.notes));
            AtomicFile.Write(Path.Combine(Root, "settings.json"), Json.Write(data.settings));
        }

        // SAVE: ordered revisions under storage gate.
        public Task SaveAsync(BackupDocument snapshot, long revision)
        {
            return Task.Run(() => SaveSnapshot(snapshot, revision));
        }

        // Keep revision checks and both writes inside the same storage gate.
        private void SaveSnapshot(BackupDocument snapshot, long revision)
        {
            gate.Wait();
            try
            {
                if (RecoveryBlocked) throw new InvalidOperationException("Storage is protected pending recovery.");
                if (revision <= savedRevision) return;
                Validation.Notes(snapshot.notes);
                Validation.Settings(snapshot.settings);
                WritePair(snapshot);
                savedRevision = revision;
            }
            finally
            {
                gate.Release();
            }
        }

        // BAK: validate both documents.
        public static BackupDocument ReadBackup(string json)
        {
            var data = Json.Read<BackupDocument>(json);
            Validation.Notes(data.notes);
            Validation.Settings(data.settings);
            return data;
        }

        // REC: apply validated restore transaction.
        public Task RestoreAsync(BackupDocument incoming, BackupDocument current, long revision)
        {
            Validation.Notes(incoming.notes);
            Validation.Settings(incoming.settings);
            return Task.Run(() => RestoreSnapshot(incoming, current, revision));
        }

        // Archive the current files before committing a replayable restore transaction.
        private void RestoreSnapshot(BackupDocument incoming, BackupDocument current, long revision)
        {
            gate.Wait();
            try
            {
                new BackupService(this).AutomaticBackup(current);
                ArchiveOriginals();
                var journal = Path.Combine(Root, "restore.pending.json");
                AtomicFile.Write(journal, Json.Write(incoming));
                RecoveryBlocked = true;
                WritePair(incoming);
                File.Delete(journal);
                savedRevision = revision;
                RecoveryBlocked = false;
            }
            finally
            {
                gate.Release();
            }
        }

        // ARCH: retain originals before transaction.
        private void ArchiveOriginals()
        {
            foreach (var name in new[] { "notes.json", "settings.json", "restore.pending.json" })
            {
                ArchiveOriginal(name);
            }
        }

        // ARCH: same-dir durable copy, including corrupt data.
        private void ArchiveOriginal(string name)
        {
            var path = Path.Combine(Root, name);
            if (!File.Exists(path)) return;
            AtomicFile.Copy(path, path + ".before-restore-" + Guid.NewGuid().ToString("N"));
        }

        // FREE: release owned resources.
        public void Dispose()
        {
            gate.Dispose();
        }
    }
}
