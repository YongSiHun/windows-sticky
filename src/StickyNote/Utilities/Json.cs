using System;
using System.IO;
using System.Web.Script.Serialization;
using StickyNote.Models;
using System.Collections.Generic;

namespace StickyNote.Utilities
{
    public static class Json
    {
        // JSON: bounded serializer.
        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = 32 * 1024 * 1024,
                RecursionLimit = 32
            };
        }

        // IO: serialize data.
        public static string Write<T>(T value)
        {
            return Serializer().Serialize(value);
        }

        // JSON: validate shape before conversion.
        public static T Read<T>(string value)
        {
            var serializer = Serializer();
            var raw = serializer.DeserializeObject(value) as Dictionary<string, object>;
            CheckShape(raw, typeof(T));
            return serializer.ConvertToType<T>(raw);
        }

        // VER: schema dispatch.
        private static void CheckShape(Dictionary<string, object> raw, Type type)
        {
            CheckVersion(raw);
            if (type == typeof(BackupDocument))
            {
                CheckBackup(raw);
                return;
            }
            if (type == typeof(NoteDocument))
            {
                CheckNotes(raw);
                return;
            }
            if (type == typeof(Settings)) Require(raw, "startWithWindows", "backgroundColor", "foregroundColor", "fontSize", "opacity", "displayMode", "language");
        }

        // VER: reject unknown/missing versions.
        private static void CheckVersion(Dictionary<string, object> raw)
        {
            if (raw == null || !raw.ContainsKey("schemaVersion")) throw new InvalidDataException("Missing schema version.");
            if (!(raw["schemaVersion"] is int)) throw new InvalidDataException("Invalid schema version.");
            if ((int)raw["schemaVersion"] != 1) throw new NotSupportedException("Unsupported schema version.");
        }

        // BAK: validate both documents.
        private static void CheckBackup(Dictionary<string, object> raw)
        {
            Require(raw, "notes", "settings");
            CheckShape(raw["notes"] as Dictionary<string, object>, typeof(NoteDocument));
            CheckShape(raw["settings"] as Dictionary<string, object>, typeof(Settings));
        }

        // NOTES: required fields before deserialization defaults.
        private static void CheckNotes(Dictionary<string, object> raw)
        {
            Require(raw, "notes");
            var notes = raw["notes"] as object[];
            if (notes == null) throw new InvalidDataException("Missing notes.");
            foreach (var item in notes)
            {
                Require(item as Dictionary<string, object>, "id", "title", "content", "backgroundColor", "foregroundColor", "fontSize", "opacity", "x", "y", "width", "height", "displayMode", "isVisible", "createdAt", "updatedAt");
            }
        }

        // REQ: reject missing object/fields.
        private static void Require(Dictionary<string, object> raw, params string[] keys)
        {
            if (raw == null) throw new InvalidDataException("Missing object.");
            foreach (var key in keys)
            {
                if (!raw.ContainsKey(key)) throw new InvalidDataException("Missing field.");
            }
        }

        // COPY: detached value.
        public static T Copy<T>(T value)
        {
            return Serializer().Deserialize<T>(Write(value));
        }
    }
}
