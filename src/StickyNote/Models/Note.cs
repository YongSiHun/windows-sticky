using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using StickyNote.Utilities;

namespace StickyNote.Models
{
    public class Observable : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value; Changed(name); return true;
        }
        protected void Changed(string name) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name)); }
    }

    // Lowercase properties deliberately match the portable JSON schema.
    public sealed class Note : Observable
    {
        private string _id = Guid.NewGuid().ToString();
        public string id { get { return _id; } set { _id = value; } }
        private string _title = "", _content = "", _background = "#FFF2AD", _foreground = "#333333", _mode = "desktop";
        private double _font = 14, _opacity = 1, _x = 100, _y = 100, _width = 280, _height = 300;
        private bool _visible = true;
        public string title { get { return _title; } set { Set(ref _title, value); } }
        public string content { get { return _content; } set { Set(ref _content, value); } }
        public string backgroundColor { get { return _background; } set { Set(ref _background, value); } }
        public string foregroundColor { get { return _foreground; } set { Set(ref _foreground, value); } }
        public double fontSize { get { return _font; } set { Set(ref _font, value); } }
        public double opacity { get { return _opacity; } set { Set(ref _opacity, value); } }
        public double x { get { return _x; } set { Set(ref _x, value); } }
        public double y { get { return _y; } set { Set(ref _y, value); } }
        public double width { get { return _width; } set { Set(ref _width, value); } }
        public double height { get { return _height; } set { Set(ref _height, value); } }
        public string displayMode { get { return _mode; } set { Set(ref _mode, value); } }
        public bool isVisible { get { return _visible; } set { Set(ref _visible, value); } }
        public string monitorDeviceName { get; set; }
        private string _createdAt = DateTime.UtcNow.ToString("o");
        public string createdAt { get { return _createdAt; } set { _createdAt = value; } }
        private string _updatedAt = DateTime.UtcNow.ToString("o");
        public string updatedAt { get { return _updatedAt; } set { _updatedAt = value; } }
        public Note Clone() { return Json.Copy(this); }
    }

    public sealed class NoteDocument
    {
        private int _schemaVersion = 1;
        public int schemaVersion { get { return _schemaVersion; } set { _schemaVersion = value; } }
        private System.Collections.Generic.List<Note> _notes = new System.Collections.Generic.List<Note>();
        public System.Collections.Generic.List<Note> notes { get { return _notes; } set { _notes = value; } }
    }

    public sealed class Settings
    {
        private int _schemaVersion = 1;
        public int schemaVersion { get { return _schemaVersion; } set { _schemaVersion = value; } }
        public bool startWithWindows { get; set; }
        private string _backgroundColor = "#FFF2AD";
        public string backgroundColor { get { return _backgroundColor; } set { _backgroundColor = value; } }
        private string _foregroundColor = "#333333";
        public string foregroundColor { get { return _foregroundColor; } set { _foregroundColor = value; } }
        private double _fontSize = 14;
        public double fontSize { get { return _fontSize; } set { _fontSize = value; } }
        private double _opacity = 1;
        public double opacity { get { return _opacity; } set { _opacity = value; } }
        private string _displayMode = "desktop";
        public string displayMode { get { return _displayMode; } set { _displayMode = value; } }
        private string _language = "ko";
        public string language { get { return _language; } set { _language = value; } }
    }
    public sealed class BackupDocument
    {
        private int _schemaVersion = 1;
        public int schemaVersion { get { return _schemaVersion; } set { _schemaVersion = value; } }
        public NoteDocument notes { get; set; }
        public Settings settings { get; set; }
    }
}
