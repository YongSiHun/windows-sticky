using System;
using System.Drawing;
using System.Windows;
using StickyNote.Utilities;
using Forms = System.Windows.Forms;

namespace StickyNote.Services
{
    public sealed class TrayService : IDisposable
    {
        private readonly Forms.NotifyIcon icon;
        private readonly Forms.ContextMenuStrip menu;
        private readonly Icon applicationIcon;
        // INIT: wire owned state and events.
        public TrayService(NoteService notes, Action newNote, Action openNotes, Action settings, Action exit)
        {
            menu = new Forms.ContextMenuStrip();
            Add("NewNote", newNote); Add("ShowAll", notes.ShowAll);
            menu.Items.Add(new Forms.ToolStripSeparator()); Add("Settings", settings); Add("Exit", exit);
            applicationIcon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            icon = new Forms.NotifyIcon { Icon = applicationIcon, Text = Loc.Get("AppTitle"), ContextMenuStrip = menu };
            icon.DoubleClick += DoubleClick; showNotes = openNotes;
            try { icon.Visible = true; }
            catch { icon.Dispose(); applicationIcon.Dispose(); menu.Dispose(); throw; }
        }
        private readonly Action showNotes;
        // UI: reopen existing editors on dispatcher.
        private void DoubleClick(object s, EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(showNotes);
        }
        // UI: add localized action.
        private void Add(string key, Action action)
        {
            menu.Items.Add(Loc.Get(key), null, (s, e) => Application.Current.Dispatcher.BeginInvoke(action));
        }
        // ERR: show localized tray notice.
        public void Notify(string key)
        {
            icon.ShowBalloonTip(5000, Loc.Get("AppTitle"), Loc.Get(key), Forms.ToolTipIcon.Warning);
        }
        // FREE: release owned resources.
        public void Dispose()
        {
            icon.Visible = false;
            icon.DoubleClick -= DoubleClick;
            icon.Dispose();
            menu.Dispose();
            applicationIcon.Dispose();
        }
    }
}
