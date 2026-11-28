using System;
using System.IO;
using System.Text;

namespace StickyNote.Utilities
{
    internal static class AtomicFile
    {
        // UTF8: shared durable text writer.
        internal static void Write(string path, string text, string previousPath = null)
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);
            Commit(path, stream => stream.Write(bytes, 0, bytes.Length), previousPath);
        }

        // ARCH: retain exact bytes, including invalid UTF-8.
        internal static void Copy(string source, string destination)
        {
            using (var input = File.OpenRead(source))
            {
                Commit(destination, output => input.CopyTo(output), null);
            }
        }

        // IO: same-dir temp, flush, replace, previous-good backup.
        private static void Commit(string path, Action<FileStream> write, string previousPath)
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    write(stream);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, previousPath ?? path + ".bak", true);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
