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
        private SettingsWindow settings;
        private NoteManagerWindow manager;
        private bool exiting, restoring, saveErrorShown;
        public NoteService Notes { get; private set; }
        public BackupService Backups { get; private set; }
        // INIT: compose services and show UI.
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                InitializeStorage(e);
                if (!StartInstance()) return;
                InitializeNotes();
                InitializeShell();
                ShowInitialWindows();
                SubscribeEvents();
                ShowRecoveryStatus();
            }
            catch (Exception error)
            {
                if (log != null) log.Write("startup", error); MessageBox.Show(Loc.Get("StartupError"), Loc.Get("AppTitle")); Cleanup(); Shutdown(1);
            }
        }
        // INIT: storage location; QA override is Debug-only.
        private void InitializeStorage(StartupEventArgs e)
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyNote");
#if DEBUG
            if (e.Args.Length == 2 && e.Args[0] == "--diagnostic-data-directory") root = Path.GetFullPath(e.Args[1]);
#endif
            log = new LogService(root);
            storage = new StorageService(root, log);
        }
        // IPC: activate existing instance.
        private bool StartInstance()
        {
            instance = new SingleInstanceService();
            if (instance.IsFirst) return true;
            if (!instance.Signal()) MessageBox.Show(Loc.Get("InstanceUnavailable"), Loc.Get("AppTitle"));
            instance.Dispose(); instance = null; Shutdown();
            return false;
        }
        // INIT: load detached data and wire autosave.
        private void InitializeNotes()
        {
            var loaded = storage.Load();
            Notes = new NoteService(loaded.settings, RequestSave, log);
            autosave = new AutosaveService(storage, Notes.Snapshot, SaveFailed);
            Backups = new BackupService(storage);
            Notes.Load(loaded.notes.notes);
        }
        // ERR: report first failure until next edit.
        private void SaveFailed(Exception error)
        {
            log.Write("autosave", error);
            if (saveErrorShown) return;
            saveErrorShown = true;
            if (tray != null) { tray.Notify("SaveError"); return; }
            MessageBox.Show(Loc.Get("SaveError"));
        }
        // INIT: optional tray and startup registration.
        private void InitializeShell()
        {
            try { tray = new TrayService(Notes, ShowManager, ShowSettings, ExitApplication); }
            catch (Exception error) { log.Write("tray-init", error); }
            if (storage.RecoveryBlocked || !Notes.Settings.startWithWindows) return;
            try { StartupService.Apply(true); }
            catch (Exception error) { log.Write("startup-refresh", error); }
        }
        // UI: retain a usable entry point without tray.
        private void ShowInitialWindows()
        {
            if (!storage.RecoveryBlocked && Notes.Notes.Count == 0) Notes.Create();
            if (tray == null || storage.RecoveryBlocked) { ShowSettings(); return; }
            if (!Notes.Notes.Any(n => n.isVisible)) ShowManager();
        }
        // EVT: dispatcher activation and display/session changes.
        private void SubscribeEvents()
        {
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
            SessionEnding += Ending;
            instance.Listen(() => Dispatcher.BeginInvoke(new Action(ShowManager)), log);
        }
        // REC: explain protected storage.
        private void ShowRecoveryStatus()
        {
            if (storage.RecoveryBlocked) { MessageBox.Show(Loc.Get("RecoveryBlocked"), Loc.Get("AppTitle")); ShowSettings(); return; }
            if (storage.Recovered) MessageBox.Show(Loc.Get("Recovered"), Loc.Get("AppTitle"));
        }
        // SAVE: request only while editable.
        public void RequestSave()
        {
            if (restoring || exiting) return;
            if (autosave != null) autosave.Request();
            saveErrorShown = false;
        }
        // SAVE: capture latest pending state.
        public Task FlushAsync()
        {
            return autosave.FlushAsync();
        }
        // EXIT: retain an exit path if tray initialization failed.
        private void SettingsClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (exiting || tray != null) return;
            e.Cancel = true;
            Dispatcher.BeginInvoke(new Action(ExitApplication));
        }
        // UI: hidden-note list with show/hide/delete controls.
        public void ShowManager()
        {
            if (exiting || restoring) return;
            if (manager == null) CreateManager();
            manager.Show(); manager.WindowState = WindowState.Normal; manager.Activate();
        }
        // UI: release the list window when closed.
        private void CreateManager()
        {
            manager = new NoteManagerWindow(Notes);
            manager.Closed += (s, e) => manager = null;
        }
        // UI: create settings once and wire fallback exit.
        private void CreateSettings()
        {
            settings = new SettingsWindow(this);
            settings.Closed += (s, e) => settings = null;
            settings.Closing += SettingsClosing;
        }
        // UI: lazy settings activation.
        public void ShowSettings()
        {
            if (exiting || restoring) return;
            if (settings == null) CreateSettings();
            settings.Show(); settings.Activate();
        }
        // REC: apply validated restore transaction.
        public async Task RestoreAsync(BackupDocument data)
        {
            if (restoring) return;
            restoring = true;
            var windows = Windows.Cast<Window>().ToArray(); SetEnabled(windows, false);
            try
            {
                try { await autosave.FlushAsync(); } catch (Exception error) { log.Write("pre-restore-flush", error); }
                await storage.RestoreAsync(data, Notes.Snapshot(), autosave.NextRevision());
                autosave.ResetAfterRestore(); Notes.Settings = data.settings; Notes.Load(data.notes.notes);
                try { StartupService.Apply(data.settings.startWithWindows); } catch (Exception error) { Report("startup-restore", error); }
                MessageBox.Show(Loc.Get("RestoreSuccess"), Loc.Get("AppTitle"));
            }
            finally { restoring = false; SetEnabled(windows, true); }
        }
        // UI: enable surviving windows after async work.
        private static void SetEnabled(Window[] windows, bool enabled)
        {
            foreach (var window in windows) if (window.IsLoaded) window.IsEnabled = enabled;
        }
        // ERR: log metadata and show localized error.
        public void Report(string operation, Exception error)
        {
            log.Write(operation, error);
            MessageBox.Show(Loc.Get("OperationError"), Loc.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        // VER: metadata-only diagnostics; status is shown inline in settings.
        internal void LogUpdateFailure(Exception error) { log.Write("update-check", error); }
        // DPI: queue live window repositioning.
        private void DisplayChanged(object sender, EventArgs e) { Dispatcher.BeginInvoke(new Action(() => { if (!exiting) Notes.RepositionAll(); })); }
        // DPI: refresh after resume.
        private void PowerChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) DisplayChanged(sender, e);
        }
        // EXIT: flush final snapshot before shutdown.
        public async void ExitApplication()
        {
            if (exiting || restoring) return;
            exiting = true;
            // Freeze editors so the final detached snapshot cannot miss new input.
            SetEnabled(Windows.Cast<Window>().ToArray(), false);
            try { await autosave.FlushAsync(); }
            catch (Exception error)
            {
                log.Write("shutdown-save", error);
                if (MessageBox.Show(Loc.Get("ExitSaveError"), Loc.Get("AppTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                { CancelExit(); return; }
            }
            Cleanup(); Shutdown();
        }
        // EXIT: resume editors after failed final save.
        private void CancelExit()
        {
            exiting = false;
            foreach (Window window in Windows) window.IsEnabled = true;
        }
        // EXIT: cancel session end if save fails.
        private void Ending(object sender, SessionEndingCancelEventArgs e)
        {
            if (restoring) { e.Cancel = true; return; }
            try { storage.SaveAsync(Notes.Snapshot(), autosave.NextRevision()).GetAwaiter().GetResult(); }
            catch (Exception error) { log.Write("session-save", error); e.Cancel = true; return; }
            exiting = true; Cleanup();
        }
        // FREE: release process-level subscriptions.
        private void Cleanup()
        {
            SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.PowerModeChanged -= PowerChanged; SessionEnding -= Ending;
            if (autosave != null) autosave.Dispose();
            if (Notes != null) Notes.Dispose();
            if (tray != null) tray.Dispose(); tray = null; if (instance != null) instance.Dispose(); instance = null;
            // Storage gate may still be held only on startup failure; leave disposal to process exit in that case.
        }
    }
}
