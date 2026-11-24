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
        public TrayService(NoteService notes, Action manager, Action settings, Action exit)
        {
            menu = new Forms.ContextMenuStrip();
            Add("NewNote", () => notes.Create()); Add("Manager", manager); Add("ShowAll", notes.ShowAll); Add("HideAll", notes.HideAll);
            menu.Items.Add(new Forms.ToolStripSeparator()); Add("Settings", settings); Add("Exit", exit);
            icon = new Forms.NotifyIcon { Icon = SystemIcons.Application, Text = Loc.Get("AppTitle"), ContextMenuStrip = menu };
            icon.DoubleClick += DoubleClick; openManager = manager;
            try { icon.Visible = true; }
            catch { icon.Dispose(); menu.Dispose(); throw; }
        }
        private readonly Action openManager;
        private void DoubleClick(object s, EventArgs e) { Application.Current.Dispatcher.BeginInvoke(openManager); }
        private void Add(string key, Action action) { menu.Items.Add(Loc.Get(key), null, (s, e) => Application.Current.Dispatcher.BeginInvoke(action)); }
        public void Notify(string key) { icon.ShowBalloonTip(5000, Loc.Get("AppTitle"), Loc.Get(key), Forms.ToolTipIcon.Warning); }
        public void Dispose() { icon.Visible = false; icon.DoubleClick -= DoubleClick; icon.Dispose(); menu.Dispose(); }
    }
}
