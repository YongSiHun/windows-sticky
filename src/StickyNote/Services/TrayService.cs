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
        public TrayService(NoteService notes, Action manager, Action settings, Action exit)
        {
            menu = new Forms.ContextMenuStrip();
            Add("NewNote", () => notes.Create()); Add("Manager", manager); Add("ShowAll", notes.ShowAll); Add("HideAll", notes.HideAll);
            menu.Items.Add(new Forms.ToolStripSeparator()); Add("Settings", settings); Add("Exit", exit);
            applicationIcon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            icon = new Forms.NotifyIcon { Icon = applicationIcon, Text = Loc.Get("AppTitle"), ContextMenuStrip = menu };
            icon.DoubleClick += DoubleClick; openManager = manager;
            try { icon.Visible = true; }
            catch { icon.Dispose(); applicationIcon.Dispose(); menu.Dispose(); throw; }
        }
        private readonly Action openManager;
        // UI: activate manager on dispatcher.
        private void DoubleClick(object s, EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(openManager);
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
