using System;
using System.IO;
using System.Linq;
using StickyNote.Utilities;

namespace StickyNote.Services
{
    public sealed class LogService
    {
        private readonly string directory;
        private readonly object gate = new object();
        // Keep metadata logs beneath the selected application data directory.
        public LogService(string root)
        {
            directory = Path.Combine(root, "logs");
        }

        // ERR: no message, stack, title or content.
        private static string ErrorCode(Exception error)
        {
            if (error == null) return "";
            var native = error as System.ComponentModel.Win32Exception;
            return error.GetType().Name + (native == null ? "" : " code=" + native.NativeErrorCode);
        }

        // GC: expire a log and its previous-good copy together.
        private void Prune()
        {
            foreach (var file in Directory.GetFiles(directory, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).Skip(6))
            {
                File.Delete(file);
                if (File.Exists(file + ".bak")) File.Delete(file + ".bak");
            }
        }

        // Deliberately record only operation and exception TYPE, never exception messages/data.
        public void Write(string operation, Exception error = null)
        {
            lock (gate)
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    Prune();
                    var path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
                    if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) return;
                    var line = DateTime.UtcNow.ToString("o") + " " + operation + " " + ErrorCode(error) + Environment.NewLine;
                    var previous = File.Exists(path) ? File.ReadAllText(path) : "";
                    AtomicFile.Write(path, previous + line);
                }
                catch (Exception)
                {
                    // Logging must never make the app unusable.
                }
            }
        }
    }
}
