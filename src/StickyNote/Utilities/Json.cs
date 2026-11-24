using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using StickyNote.Models;
using System.Collections.Generic;

namespace StickyNote.Utilities
{
    public static class Json
    {
        private static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024, RecursionLimit = 32 }; }
        public static string Write<T>(T value) { return Serializer().Serialize(value); }
        public static T Read<T>(string value)
        {
            var raw = Serializer().DeserializeObject(value) as Dictionary<string, object>;
            CheckShape(raw, typeof(T));
            return Serializer().Deserialize<T>(value);
        }
        private static void CheckShape(Dictionary<string, object> raw, Type type)
        {
            if (raw == null || !raw.ContainsKey("schemaVersion") || !(raw["schemaVersion"] is int)) throw new InvalidDataException("Missing schema version.");
            if ((int)raw["schemaVersion"] != 1) throw new NotSupportedException("Unsupported schema version.");
            if (type == typeof(BackupDocument))
            {
                Require(raw, "notes", "settings");
                CheckShape(raw["notes"] as Dictionary<string, object>, typeof(NoteDocument));
                CheckShape(raw["settings"] as Dictionary<string, object>, typeof(Settings));
            }
            else if (type == typeof(NoteDocument))
            {
                Require(raw, "notes"); var notes = raw["notes"] as object[];
                if (notes == null) throw new InvalidDataException("Missing notes.");
                foreach (var item in notes)
                    Require(item as Dictionary<string, object>, "id", "title", "content", "backgroundColor", "foregroundColor", "fontSize", "opacity", "x", "y", "width", "height", "displayMode", "isVisible", "createdAt", "updatedAt");
            }
            else if (type == typeof(Settings)) Require(raw, "startWithWindows", "backgroundColor", "foregroundColor", "fontSize", "opacity", "displayMode", "language");
        }
        private static void Require(Dictionary<string, object> raw, params string[] keys)
        {
            if (raw == null) throw new InvalidDataException("Missing object.");
            foreach (var key in keys) if (!raw.ContainsKey(key)) throw new InvalidDataException("Missing field.");
        }
        public static T Copy<T>(T value) { return Serializer().Deserialize<T>(Write(value)); }
    }
    public static class Validation
    {
        public static readonly string[] Colors = { "#FFF2AD", "#D6EDB0", "#C5E5F5", "#F8D2DF", "#E3D5F5", "#FFFFFF", "#DDDDDD" };
        public static bool Color(string value) { return value != null && Regex.IsMatch(value, "^#[0-9A-Fa-f]{6}$"); }
        public static bool Mode(string mode) { return mode == "normal" || mode == "topmost" || mode == "desktop"; }
        public static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        public static void Settings(Settings s)
        {
            if (s == null || s.schemaVersion != 1 || !Color(s.backgroundColor) || !Color(s.foregroundColor) || !Mode(s.displayMode) ||
                !Finite(s.fontSize) || s.fontSize < 8 || s.fontSize > 72 || !Finite(s.opacity) || s.opacity < .3 || s.opacity > 1 ||
                (s.language != "ko" && s.language != "en")) throw new InvalidDataException("Invalid settings.");
        }
        public static void Notes(NoteDocument d)
        {
            if (d == null || d.schemaVersion != 1 || d.notes == null || d.notes.Count > 10000) throw new InvalidDataException("Invalid note document.");
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in d.notes)
            {
                Guid id; DateTimeOffset created, updated;
                if (n == null || !Guid.TryParse(n.id, out id) || !ids.Add(n.id) || n.title == null || n.content == null ||
                    n.title.Length > 500 || n.content.Length > 1000000 || !Color(n.backgroundColor) || !Color(n.foregroundColor) || !Mode(n.displayMode) ||
                    !Finite(n.fontSize) || n.fontSize < 8 || n.fontSize > 72 || !Finite(n.opacity) || n.opacity < .3 || n.opacity > 1 ||
                    !Finite(n.x) || !Finite(n.y) || Math.Abs(n.x) > 1000000 || Math.Abs(n.y) > 1000000 ||
                    !Finite(n.width) || !Finite(n.height) || n.width < 200 || n.width > 10000 || n.height < 160 || n.height > 10000 ||
                    !DateTimeOffset.TryParse(n.createdAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out created) || created.Offset != TimeSpan.Zero ||
                    !DateTimeOffset.TryParse(n.updatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out updated) || updated.Offset != TimeSpan.Zero)
                    throw new InvalidDataException("Invalid note fields.");
            }
        }
    }
}
