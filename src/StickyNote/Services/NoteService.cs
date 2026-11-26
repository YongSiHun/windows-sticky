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
        private readonly System.Windows.Threading.Dispatcher dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        private readonly Action changed;
        private readonly LogService log;
        public Settings Settings { get; set; }
        // INIT: wire owned state and events.
        public NoteService(Settings settings, Action changed, LogService log)
        {
            Settings = settings;
            this.changed = changed;
            this.log = log;
        }
        // LOAD: validate data before use.
        public void Load(IEnumerable<Note> notes)
        {
            foreach (var window in windows.Values.ToArray()) window.Destroy(); windows.Clear();
            foreach (var note in Notes) note.PropertyChanged -= Changed;
            Notes.Clear();
            foreach (var note in notes) { Notes.Add(note); note.PropertyChanged += Changed; }
            foreach (var note in Notes.Where(n => n.isVisible)) ShowWindow(note);
        }
        // EVT: publish property change.
        private void Changed(object sender, PropertyChangedEventArgs e)
        {
            ((Note)sender).updatedAt = DateTime.UtcNow.ToString("o"); changed();
        }
        // NOTE: new identity and defaults or duplicate.
        public Note Create(Note original = null)
        {
            var note = original != null ? original.Clone() : new Note { title = Loc.Get("NewNote"), backgroundColor = Settings.backgroundColor, foregroundColor = Settings.foregroundColor, fontSize = Settings.fontSize, opacity = Settings.opacity, displayMode = Settings.displayMode };
            note.id = Guid.NewGuid().ToString(); note.createdAt = note.updatedAt = DateTime.UtcNow.ToString("o");
            if (original != null) { note.x += 24; note.y += 24; }
            note.isVisible = true; Notes.Add(note); note.PropertyChanged += Changed; ShowWindow(note); changed(); return note;
        }
        // UI: reuse existing note window.
        private void ShowWindow(Note note)
        {
            NoteWindow window;
            if (windows.TryGetValue(note.id, out window)) { window.Show(); return; }
            window = new NoteWindow(note, this, log);
            windows.Add(note.id, window);
            window.Show();
        }
        // UI: mark visible and open editor.
        public void Show(Note note)
        {
            note.isVisible = true;
            ShowWindow(note);
        }
        // UI: release hidden editor resources.
        public void Hide(Note note)
        {
            note.isVisible = false;
            NoteWindow window; if (windows.TryGetValue(note.id, out window)) { windows.Remove(note.id); window.Destroy(); }
        }
        // DEL: remove note after caller confirmation.
        public void Delete(Note note)
        {
            Hide(note);
            note.PropertyChanged -= Changed;
            Notes.Remove(note);
            changed();
        }
        // UI: show each note.
        public void ShowAll()
        {
            foreach (var note in Notes) Show(note);
        }
        // UI: hide each note.
        public void HideAll()
        {
            foreach (var note in Notes) Hide(note);
        }
        // COPY: capture detached state on owner dispatcher.
        public BackupDocument Snapshot()
        {
            dispatcher.VerifyAccess();
            return new BackupDocument { notes = new NoteDocument { notes = Notes.Select(n => n.Clone()).ToList() }, settings = Json.Copy(Settings) };
        }
        // DPI: reposition live note windows.
        public void RepositionAll()
        {
            foreach (var window in windows.Values) window.Reposition();
        }
        // FREE: release owned resources.
        public void Dispose()
        {
            foreach (var window in windows.Values.ToArray()) window.Destroy(); windows.Clear();
            foreach (var note in Notes) note.PropertyChanged -= Changed;
        }
    }
}
