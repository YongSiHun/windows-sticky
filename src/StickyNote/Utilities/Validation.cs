using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using StickyNote.Models;

namespace StickyNote.Utilities
{
    public static class Validation
    {
        public static readonly string[] Colors = { "#FFF2AD", "#D6EDB0", "#C5E5F5", "#F8D2DF", "#E3D5F5", "#FFFFFF", "#DDDDDD" };
        public static readonly string[] Palette = {
            "#FFF2AD", "#D6EDB0", "#C5E5F5", "#F8D2DF", "#E3D5F5", "#FFFFFF", "#DDDDDD", "#333333",
            "#FFE082", "#AED581", "#81D4FA", "#F48FB1", "#CE93D8", "#FFCCBC", "#B0BEC5", "#000000",
            "#FFC107", "#4CAF50", "#2196F3", "#E91E63", "#9C27B0", "#FF5722", "#607D8B", "#795548",
            "#FF8F00", "#1B5E20", "#0D47A1", "#880E4F", "#4A148C", "#BF360C", "#37474F", "#3E2723"
        };
        private static readonly Regex ColorPattern = new Regex("^#[0-9A-Fa-f]{6}$");
        private static readonly string[] Modes = { "normal", "topmost", "desktop" };

        // HEX: schema color.
        public static bool Color(string value)
        {
            return value != null && ColorPattern.IsMatch(value);
        }
        // MODE: persisted request.
        public static bool Mode(string mode)
        {
            return Array.IndexOf(Modes, mode) >= 0;
        }
        // NUM: reject NaN/Inf.
        public static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
        // RNG: inclusive finite range.
        private static bool Range(double value, double min, double max)
        {
            return Finite(value) && value >= min && value <= max;
        }
        // REQ: reject invalid fields.
        private static void Require(bool valid)
        {
            if (!valid) throw new InvalidDataException("Invalid document fields.");
        }
        // STYLE: shared note/default bounds.
        private static void Style(string background, string foreground, string mode, double font, double opacity)
        {
            Require(Color(background));
            Require(Color(foreground));
            Require(Mode(mode));
            Require(Range(font, 8, 72));
            Require(Range(opacity, .3, 1));
        }
        // CFG: validate defaults.
        public static void Settings(Settings settings)
        {
            Require(settings != null);
            Require(settings.schemaVersion == 1);
            Require(settings.language == "ko" || settings.language == "en");
            Require(FontName(settings.fontFamily));
            Style(settings.backgroundColor, settings.foregroundColor, settings.displayMode, settings.fontSize, settings.opacity);
        }
        // DOC: bounded collection, unique IDs.
        public static void Notes(NoteDocument document)
        {
            Require(document != null);
            Require(document.schemaVersion == 1);
            Require(document.notes != null);
            Require(document.notes.Count <= 10000);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var note in document.notes) ValidateNote(note, ids);
        }
        // NOTE: identity, text, style, geometry, UTC.
        private static void ValidateNote(Note note, HashSet<string> ids)
        {
            Require(note != null);
            Guid id;
            Require(Guid.TryParse(note.id, out id));
            Require(ids.Add(note.id));
            Require(Text(note.title, 500));
            Require(Text(note.content, 1000000));
            Require(FontName(note.fontFamily));
            Style(note.backgroundColor, note.foregroundColor, note.displayMode, note.fontSize, note.opacity);
            Require(Range(note.x, -1000000, 1000000));
            Require(Range(note.y, -1000000, 1000000));
            Require(Range(note.width, 200, 10000));
            Require(Range(note.height, 160, 10000));
            Require(Utc(note.createdAt));
            Require(Utc(note.updatedAt));
        }
        // TXT: bounded non-null text.
        private static bool Text(string value, int limit)
        {
            return value != null && value.Length <= limit;
        }
        // FONT: absent typefaces use WPF fallback; persisted names remain portable.
        private static bool FontName(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 200 && value.IndexOfAny(new[] { '\r', '\n', '\0' }) < 0;
        }
        // UTC: round-trip timestamp.
        private static bool Utc(string value)
        {
            DateTimeOffset timestamp;
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp) && timestamp.Offset == TimeSpan.Zero;
        }
    }
}
