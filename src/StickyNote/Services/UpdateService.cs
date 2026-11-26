using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StickyNote.Services
{
    internal static class UpdateService
    {
        internal const string DownloadsUrl = "https://github.com/YongSiHun/windows-sticky/actions/workflows/build.yml";
        internal static Version Current { get { return typeof(App).Assembly.GetName().Version; } }

        // VER: explicit user request; bounded I/O off the dispatcher.
        internal static Task<Version> CheckAsync()
        {
            return Task.Run(() => ReadVersion("https://raw.githubusercontent.com/YongSiHun/windows-sticky/HEAD/VERSION"));
        }

        // HTTP: version text only; never send note data.
        private static Version ReadVersion(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 8000; request.ReadWriteTimeout = 8000;
            request.UserAgent = "StickyNote/" + Current.ToString(3);
            using (var response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var buffer = new char[64];
                int count = reader.ReadBlock(buffer, 0, buffer.Length);
                if (count == buffer.Length) throw new InvalidDataException("Version response too large.");
                return ParseVersion(new string(buffer, 0, count).Trim());
            }
        }

        // VER: accept only three numeric parts.
        internal static Version ParseVersion(string text)
        {
            if (text == null || !Regex.IsMatch(text, "^[0-9]+\\.[0-9]+\\.[0-9]+$")) throw new InvalidDataException("Invalid version.");
            Version version;
            if (!Version.TryParse(text, out version)) throw new InvalidDataException("Invalid version.");
            return new Version(version.Major, version.Minor, version.Build, 0);
        }
    }
}
