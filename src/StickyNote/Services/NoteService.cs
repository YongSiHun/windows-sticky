using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using StickyNote.Models;
using StickyNote.Utilities;
using StickyNote.Views;

namespace StickyNote.Services
{
    public sealed class NoteService : IDisposable
    {
        private ObservableCollection<Note> _notes = new ObservableCollection<Note>();
        public ObservableCollection<Note> Notes { get { return _notes; } }
        private readonly Dictionary<string, NoteWindow> windows = new Dictionary<string, NoteWindow>();
        private readonly Action changed;
        private readonly LogService log;
        public Settings Settings { get; set; }
        public event EventHandler OpenManager;
        public NoteService(Settings settings, Action changed, LogService log) { Settings = settings; this.changed = changed; this.log = log; }
        public void Load(IEnumerable<Note> notes)
        {
            foreach (var window in windows.Values.ToArray()) window.Destroy(); windows.Clear();
            foreach (var note in Notes) note.PropertyChanged -= Changed;
            Notes.Clear();
            foreach (var note in notes) { Notes.Add(note); note.PropertyChanged += Changed; }
            foreach (var note in Notes.Where(n => n.isVisible)) ShowWindow(note);
        }
        private void Changed(object sender, PropertyChangedEventArgs e)
        {
            ((Note)sender).updatedAt = DateTime.UtcNow.ToString("o"); changed();
        }
        public Note Create(Note original = null)
        {
            var note = original != null ? original.Clone() : new Note { title = Loc.Get("NewNote"), backgroundColor = Settings.backgroundColor, foregroundColor = Settings.foregroundColor, fontSize = Settings.fontSize, opacity = Settings.opacity, displayMode = Settings.displayMode };
            note.id = Guid.NewGuid().ToString(); note.createdAt = note.updatedAt = DateTime.UtcNow.ToString("o");
            if (original != null) { note.x += 24; note.y += 24; }
            note.isVisible = true; Notes.Add(note); note.PropertyChanged += Changed; ShowWindow(note); changed(); return note;
        }
        private void ShowWindow(Note note)
        {
            if (!windows.ContainsKey(note.id))
            {
                var window = new NoteWindow(note, this, log); windows.Add(note.id, window); window.Show();
            }
            else windows[note.id].Show();
        }
        public void Show(Note note) { note.isVisible = true; ShowWindow(note); }
        public void Hide(Note note)
        {
            note.isVisible = false;
            NoteWindow window; if (windows.TryGetValue(note.id, out window)) { windows.Remove(note.id); window.Destroy(); }
        }
        public void Delete(Note note) { Hide(note); note.PropertyChanged -= Changed; Notes.Remove(note); changed(); }
        public void ShowAll() { foreach (var note in Notes) Show(note); }
        public void HideAll() { foreach (var note in Notes) Hide(note); }
        public void Manage() { if (OpenManager != null) OpenManager(this, EventArgs.Empty); }
        public BackupDocument Snapshot()
        {
            return new BackupDocument { notes = new NoteDocument { notes = Notes.Select(n => n.Clone()).ToList() }, settings = Json.Copy(Settings) };
        }
        public void RepositionAll() { foreach (var window in windows.Values) window.Reposition(); }
        public void Dispose()
        {
            foreach (var window in windows.Values.ToArray()) window.Destroy(); windows.Clear();
            foreach (var note in Notes) note.PropertyChanged -= Changed;
        }
    }
}
