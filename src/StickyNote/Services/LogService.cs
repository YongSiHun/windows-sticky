using System;
using System.IO;
using System.Linq;

namespace StickyNote.Services
{
    public sealed class LogService
    {
        private readonly string directory;
        private readonly object gate = new object();
        public LogService(string root) { directory = Path.Combine(root, "logs"); }
        // Deliberately record only operation and exception TYPE, never exception messages/data.
        public void Write(string operation, Exception error = null)
        {
            lock (gate) try
            {
                Directory.CreateDirectory(directory);
                foreach (var file in Directory.GetFiles(directory, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).Skip(6)) File.Delete(file);
                var path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd") + ".log");
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) return;
                var native = error as System.ComponentModel.Win32Exception;
                File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " " + operation + " " + (error == null ? "" : error.GetType().Name) +
                    (native == null ? "" : " code=" + native.NativeErrorCode) + Environment.NewLine);
            }
            catch (Exception) { /* Logging must never make the app unusable. */ }
        }
    }
}
