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
        private bool exiting, restoring, saveErrorShown;
        private bool? installationStartupChoice;
        public NoteService Notes { get; private set; }
        public BackupService Backups { get; private set; }
        // INIT: compose services and show UI.
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (e.Args.Length == 1 && e.Args[0] == "--shutdown")
            {
                ShutdownForUninstall();
                return;
            }
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
                HandleStartupFailure(error);
            }
        }

        // Report startup failure even when storage logging could not initialize.
        private void HandleStartupFailure(Exception error)
        {
            if (log != null) log.Write("startup", error);
            MessageBox.Show(Loc.Get("StartupError"), Loc.Get("AppTitle"));
            Cleanup();
            Shutdown(1);
        }

        // UNINSTALL: do not load storage, create notes or register startup in the helper.
        private void ShutdownForUninstall()
        {
            try
            {
                using (var running = new SingleInstanceService())
                {
                    Shutdown(running.RequestExit() ? 0 : 1);
                }
            }
            catch
            {
                Shutdown(1);
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
            instance.Dispose();
            instance = null;
            Shutdown();
            return false;
        }

        // INIT: load detached data and wire autosave.
        private void InitializeNotes()
        {
            var loaded = storage.Load();
            ReadInstallationStartup(loaded.settings);
            Notes = new NoteService(loaded.settings, RequestSave, log);
            autosave = new AutosaveService(storage, Notes.Snapshot, SaveFailed);
            Backups = new BackupService(storage);
            Notes.Load(loaded.notes.notes);
            if (installationStartupChoice.HasValue) RequestSave();
        }

        // Apply an explicit installer choice while preserving settings when none exists.
        internal static void ApplyInstallationStartup(Settings settings, bool? choice)
        {
            if (choice.HasValue) settings.startWithWindows = choice.Value;
        }

        // Read installation preferences only when storage remains editable.
        private void ReadInstallationStartup(Settings settings)
        {
            if (storage.RecoveryBlocked) return;
            try
            {
                installationStartupChoice = StartupService.InstallationChoice();
            }
            catch (Exception error)
            {
                log.Write("startup-install-choice", error);
            }
            ApplyInstallationStartup(settings, installationStartupChoice);
        }

        // ERR: report first failure until next edit.
        private void SaveFailed(Exception error)
        {
            log.Write("autosave", error);
            if (saveErrorShown) return;
            saveErrorShown = true;
            if (tray != null)
            {
                tray.Notify("SaveError");
                return;
            }
            MessageBox.Show(Loc.Get("SaveError"));
        }

        // INIT: optional tray and startup registration.
        private void InitializeShell()
        {
            try
            {
                tray = new TrayService(Notes, NewNote, OpenNotes, ShowSettings, ExitApplication);
            }
            catch (Exception error)
            {
                log.Write("tray-init", error);
            }
            if (storage.RecoveryBlocked) return;
            try
            {
                StartupService.Apply(Notes.Settings.startWithWindows);
                CompleteInstallationStartup();
            }
            catch (Exception error)
            {
                log.Write("startup-refresh", error);
                MessageBox.Show(Loc.Get("StartupRegistrationFailed"), Loc.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // SETUP: retain the installer choice until settings are durably saved.
        private async void CompleteInstallationStartup()
        {
            if (!installationStartupChoice.HasValue) return;
            try
            {
                await FlushAsync();
                StartupService.ClearInstallationChoice();
                installationStartupChoice = null;
            }
            catch (Exception error)
            {
                log.Write("startup-install-save", error);
                MessageBox.Show(Loc.Get("StartupRegistrationFailed"), Loc.Get("AppTitle"));
            }
        }

        // UI: retain a usable entry point without tray.
        private void ShowInitialWindows()
        {
            if (!storage.RecoveryBlocked) OpenNotes();
            if (tray == null || storage.RecoveryBlocked) ShowSettings();
        }

        // EVT: dispatcher activation and display/session changes.
        private void SubscribeEvents()
        {
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
            SessionEnding += Ending;
            instance.Listen(() => Dispatcher.Invoke(new Action(OpenNotes)), log,
            () => Dispatcher.BeginInvoke(new Action(ExitApplication)));
        }

        // REC: explain protected storage.
        private void ShowRecoveryStatus()
        {
            if (storage.RecoveryBlocked)
            {
                MessageBox.Show(Loc.Get("RecoveryBlocked"), Loc.Get("AppTitle"));
                ShowSettings();
                return;
            }
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

        // UI: launch restores all saved notes, creating one only when there are none.
        public void OpenNotes()
        {
            if (exiting || restoring) return;
            if (storage.RecoveryBlocked)
            {
                ShowSettings();
                return;
            }
            if (Notes.Notes.Count == 0)
            {
                Notes.Create();
                return;
            }
            Notes.ShowAll();
        }

        // UI: explicit plus/tray command adds a note.
        public void NewNote()
        {
            if (exiting || restoring) return;
            if (storage.RecoveryBlocked)
            {
                ShowSettings();
                return;
            }
            Notes.Create();
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
            settings.Show();
            settings.Activate();
        }

        // REC: apply validated restore transaction.
        public async Task RestoreAsync(BackupDocument data)
        {
            if (restoring) return;
            restoring = true;
            var windows = Windows.Cast<Window>().ToArray();
            SetEnabled(windows, false);
            try
            {
                await FlushBeforeRestoreAsync();
                await storage.RestoreAsync(data, Notes.Snapshot(), autosave.NextRevision());
                autosave.ResetAfterRestore();
                Notes.Settings = data.settings;
                Notes.Load(data.notes.notes);
                ApplyRestoredStartup(data.settings);
                MessageBox.Show(Loc.Get("RestoreSuccess"), Loc.Get("AppTitle"));
            }
            finally
            {
                restoring = false;
                SetEnabled(windows, true);
            }
        }

        // A failed pre-restore save must not prevent recovery from a valid backup.
        private async Task FlushBeforeRestoreAsync()
        {
            try
            {
                await autosave.FlushAsync();
            }
            catch (Exception error)
            {
                log.Write("pre-restore-flush", error);
            }
        }

        // Report registration failures independently of the completed data restore.
        private void ApplyRestoredStartup(Settings restoredSettings)
        {
            try
            {
                StartupService.Apply(restoredSettings.startWithWindows);
            }
            catch (Exception error)
            {
                Report("startup-restore", error);
            }
        }

        // UI: enable surviving windows after async work.
        private static void SetEnabled(Window[] windows, bool enabled)
        {
            foreach (var window in windows)
            {
                if (!window.IsLoaded) continue;
                window.IsEnabled = enabled;
            }
        }

        // ERR: log metadata and show localized error.
        public void Report(string operation, Exception error)
        {
            log.Write(operation, error);
            MessageBox.Show(Loc.Get("OperationError"), Loc.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // VER: metadata-only diagnostics; status is shown inline in settings.
        internal void LogUpdateFailure(Exception error)
        {
            log.Write("update-check", error);
        }

        // DPI: queue live window repositioning.
        private void DisplayChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!exiting) Notes.RepositionAll();
            }));
        }

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
            try
            {
                await autosave.FlushAsync();
            }
            catch (Exception error)
            {
                log.Write("shutdown-save", error);
                if (MessageBox.Show(Loc.Get("ExitSaveError"), Loc.Get("AppTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                {
                    CancelExit();
                    return;
                }
            }
            Cleanup();
            Shutdown();
        }

        // EXIT: resume editors after failed final save.
        private void CancelExit()
        {
            exiting = false;
            foreach (Window window in Windows)
            {
                window.IsEnabled = true;
            }
        }

        // EXIT: cancel session end if save fails.
        private void Ending(object sender, SessionEndingCancelEventArgs e)
        {
            if (restoring)
            {
                e.Cancel = true;
                return;
            }
            try
            {
                storage.SaveAsync(Notes.Snapshot(), autosave.NextRevision()).GetAwaiter().GetResult();
            }
            catch (Exception error)
            {
                log.Write("session-save", error);
                e.Cancel = true;
                return;
            }
            exiting = true;
            Cleanup();
        }

        // FREE: release process-level subscriptions.
        private void Cleanup()
        {
            SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            SystemEvents.PowerModeChanged -= PowerChanged;
            SessionEnding -= Ending;
            if (autosave != null) autosave.Dispose();
            if (Notes != null) Notes.Dispose();
            if (tray != null) tray.Dispose();
            tray = null;
            if (instance != null) instance.Dispose();
            instance = null;
            // Storage gate may still be held only on startup failure; leave disposal to process exit in that case.
        }
    }
}
