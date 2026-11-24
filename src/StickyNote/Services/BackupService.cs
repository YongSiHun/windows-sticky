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
        public BackupService(StorageService storage) { this.storage = storage; }
        public void Export(string path, BackupDocument snapshot)
        {
            string full = Path.GetFullPath(path);
            foreach (var name in new[] { "notes.json", "notes.json.bak", "settings.json", "settings.json.bak", "restore.pending.json", "restore.pending.json.bak" })
                if (string.Equals(full, Path.Combine(storage.Root, name), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Cannot export over live data.");
            Validation.Notes(snapshot.notes); Validation.Settings(snapshot.settings);
            StorageService.AtomicWrite(path, Json.Write(snapshot));
        }
        public BackupDocument Import(string path)
        {
            if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Backup too large.");
            return StorageService.ReadBackup(File.ReadAllText(path));
        }
        public void AutomaticBackup(BackupDocument current)
        {
            string directory = Path.Combine(storage.Root, "backups"); Directory.CreateDirectory(directory);
            Export(Path.Combine(directory, "backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".json"), current);
            foreach (var file in Directory.GetFiles(directory, "backup-*.json").OrderByDescending(File.GetLastWriteTimeUtc).Skip(10)) File.Delete(file);
        }
    }
}
