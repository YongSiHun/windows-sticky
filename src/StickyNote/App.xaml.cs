using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using StickyNote.Models;
using StickyNote.Services;
using StickyNote.Utilities;
using StickyNote.Views;

namespace StickyNote
{
    public partial class App : Application
    {
        private SingleInstanceService instance;
        private StorageService storage;
        private LogService log;
        private AutosaveService autosave;
        private TrayService tray;
        private NoteManagerWindow manager;
        private SettingsWindow settings;
        private bool exiting, restoring, saveErrorShown;
        public NoteService Notes { get; private set; }
        public BackupService Backups { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyNote");
#if DEBUG
                // Isolated QA without touching real user data. Not compiled into Release.
                if (e.Args.Length == 2 && e.Args[0] == "--diagnostic-data-directory") root = Path.GetFullPath(e.Args[1]);
#endif
                log = new LogService(root);
                instance = new SingleInstanceService();
                if (!instance.IsFirst)
                {
                    if (!instance.Signal()) MessageBox.Show(Loc.Get("InstanceUnavailable"), Loc.Get("AppTitle"));
                    instance.Dispose(); instance = null; Shutdown(); return;
                }
                storage = new StorageService(root, log);
                var loaded = storage.Load(); Loc.Culture = System.Globalization.CultureInfo.GetCultureInfo(loaded.settings.language);
                Notes = new NoteService(loaded.settings, RequestSave, log);
                autosave = new AutosaveService(storage, Notes.Snapshot, error =>
                {
                    log.Write("autosave", error);
                    if (!saveErrorShown) { saveErrorShown = true; if (tray != null) tray.Notify("SaveError"); else MessageBox.Show(Loc.Get("SaveError")); }
                });
                Backups = new BackupService(storage); Notes.OpenManager += OpenManager;
                try { tray = new TrayService(Notes, ShowManager, ShowSettings, ExitFromTray); }
                catch (Exception error) { log.Write("tray-init", error); }
                Notes.Load(loaded.notes.notes);
                if (!storage.RecoveryBlocked && loaded.notes.notes.Count == 0) Notes.Create();
                if (tray == null || !Notes.Notes.Any(n => n.isVisible) || storage.RecoveryBlocked) ShowManager();
                if (loaded.settings.startWithWindows) try { StartupService.Apply(true); } catch (Exception error) { log.Write("startup-refresh", error); }
                SystemEvents.DisplaySettingsChanged += DisplayChanged; SystemEvents.PowerModeChanged += PowerChanged;
                SessionEnding += Ending;
                instance.Listen(() => Dispatcher.BeginInvoke(new Action(ShowManager)), log);
                if (storage.RecoveryBlocked) { MessageBox.Show(Loc.Get("RecoveryBlocked"), Loc.Get("AppTitle")); ShowSettings(); }
                else if (storage.Recovered) MessageBox.Show(Loc.Get("Recovered"), Loc.Get("AppTitle"));
            }
            catch (Exception error)
            {
                if (log != null) log.Write("startup", error); MessageBox.Show(Loc.Get("StartupError"), Loc.Get("AppTitle")); Cleanup(); Shutdown(1);
            }
        }
        private void OpenManager(object sender, EventArgs e) { ShowManager(); }
        public void RequestSave() { if (!restoring && !exiting) { if (autosave != null) autosave.Request(); saveErrorShown = false; } }
        public Task FlushAsync() { return autosave.FlushAsync(); }
        public void ShowManager()
        {
            if (exiting || restoring) return;
            if (manager == null)
            {
                manager = new NoteManagerWindow(Notes); manager.Closed += (s, e) => manager = null;
                if (tray == null)
                {
                    manager.Closing += (s, e) => { if (!exiting) { e.Cancel = true; Dispatcher.BeginInvoke(new Action(ExitFromTray)); } };
                }
            }
            manager.Show(); manager.WindowState = WindowState.Normal; manager.Activate();
        }
        public void ShowSettings()
        {
            if (exiting || restoring) return;
            if (settings == null) { settings = new SettingsWindow(this); settings.Closed += (s, e) => settings = null; }
            settings.Show(); settings.Activate();
        }
        public async Task RestoreAsync(BackupDocument data)
        {
            if (restoring) return;
            restoring = true;
            var windows = Windows.Cast<Window>().ToArray(); foreach (var w in windows) w.IsEnabled = false;
            try
            {
                try { await autosave.FlushAsync(); } catch (Exception error) { log.Write("pre-restore-flush", error); }
                await storage.RestoreAsync(data, Notes.Snapshot(), autosave.NextRevision());
                autosave.ResetAfterRestore(); Notes.Settings = data.settings; Notes.Load(data.notes.notes);
                try { StartupService.Apply(data.settings.startWithWindows); } catch (Exception error) { Report("startup-restore", error); }
                MessageBox.Show(Loc.Get("RestoreSuccess"), Loc.Get("AppTitle"));
            }
            finally { restoring = false; foreach (var w in windows) if (w.IsLoaded) w.IsEnabled = true; }
        }
        public void Report(string operation, Exception error) { log.Write(operation, error); MessageBox.Show(Loc.Get("OperationError"), Loc.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        private void DisplayChanged(object sender, EventArgs e) { Dispatcher.BeginInvoke(new Action(() => { if (!exiting) Notes.RepositionAll(); })); }
        private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Resume) DisplayChanged(sender, e); }
        private async void ExitFromTray()
        {
            if (exiting || restoring) return;
            exiting = true;
            // Freeze editors so the final detached snapshot cannot miss new input.
            foreach (Window window in Windows) window.IsEnabled = false;
            try { await autosave.FlushAsync(); }
            catch (Exception error)
            {
                log.Write("shutdown-save", error);
                if (MessageBox.Show(Loc.Get("ExitSaveError"), Loc.Get("AppTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                { exiting = false; foreach (Window window in Windows) window.IsEnabled = true; return; }
            }
            Cleanup(); Shutdown();
        }
        private void Ending(object sender, SessionEndingCancelEventArgs e)
        {
            if (restoring) { e.Cancel = true; return; }
            try { storage.SaveAsync(Notes.Snapshot(), autosave.NextRevision()).GetAwaiter().GetResult(); }
            catch (Exception error) { log.Write("session-save", error); e.Cancel = true; return; }
            exiting = true; Cleanup();
        }
        private void Cleanup()
        {
            SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.PowerModeChanged -= PowerChanged; SessionEnding -= Ending;
            if (autosave != null) autosave.Dispose();
            if (Notes != null) { Notes.OpenManager -= OpenManager; Notes.Dispose(); }
            if (tray != null) tray.Dispose(); tray = null; if (instance != null) instance.Dispose(); instance = null;
            // Storage gate may still be held only on startup failure; leave disposal to process exit in that case.
        }
    }
}
