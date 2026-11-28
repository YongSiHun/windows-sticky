using System;
using System.IO;
using System.Linq;
using StickyNote.Models;
using StickyNote.Utilities;

namespace StickyNote.Services
{
    public sealed class BackupService
    {
        private readonly StorageService storage;
        private readonly Func<DateTime> utcNow;
        // Use the system UTC clock when generating automatic backup names.
        public BackupService(StorageService storage) : this(storage, () => DateTime.UtcNow)
        {
        }

        // CLK: deterministic collision regression.
        internal BackupService(StorageService storage, Func<DateTime> utcNow)
        {
            this.storage = storage;
            this.utcNow = utcNow;
        }

        // BAK: validate before atomic export.
        public void Export(string path, BackupDocument snapshot)
        {
            string full = Path.GetFullPath(path);
            if (IsLivePath(full)) throw new InvalidOperationException("Cannot export over live data.");
            Validation.Notes(snapshot.notes);
            Validation.Settings(snapshot.settings);
            AtomicFile.Write(path, Json.Write(snapshot));
        }

        // PATH: protect active documents and recovery files.
        private bool IsLivePath(string full)
        {
            return new[]
            {
                "notes.json", "notes.json.bak", "settings.json", "settings.json.bak", "restore.pending.json", "restore.pending.json.bak"
            }
            .Any(name => string.Equals(full, Path.Combine(storage.Root, name), StringComparison.OrdinalIgnoreCase));
        }

        // BAK: bound file size before validation.
        public BackupDocument Import(string path)
        {
            if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Backup too large.");
            return StorageService.ReadBackup(File.ReadAllText(path));
        }

        // BAK: unique names; retain latest ten.
        public void AutomaticBackup(BackupDocument current)
        {
            string directory = Path.Combine(storage.Root, "backups");
            Directory.CreateDirectory(directory);
            Export(Path.Combine(directory, "backup-" + utcNow().ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N") + ".json"), current);
            foreach (var file in Directory.GetFiles(directory, "backup-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Skip(10))
            {
                File.Delete(file);
            }
        }
    }
}
