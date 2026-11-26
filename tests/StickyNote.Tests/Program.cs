using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using StickyNote.Models;
using StickyNote.Services;
using StickyNote.Utilities;

[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace StickyNote.Tests
{
    internal static class Program
    {
        private static int failures, passed;
        private static string root;
        // QA: run independent regression cases.
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--signal-instance") return SignalInstance(args[1]);
            root = Path.Combine(Environment.CurrentDirectory, "TestResults", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Run("UTF-8 JSON round trip and UTC", JsonRoundTrip);
            Run("schema migration boundary rejects unknown and missing versions", SchemaBoundary);
            Run("invalid settings and note fields rejected", InvalidFields);
            Run("atomic save retains previous-good data", AtomicSave);
            Run("failed replace leaves original intact", FailedReplace);
            Run("corruption recovers backup and archives original", CorruptionRecovery);
            Run("unrecoverable and future schemas block writes", BlockedRecovery);
            Run("concurrent revisions cannot overwrite latest snapshot", ConcurrentRevisions);
            Run("backup export import validation and restore transaction", BackupRestore);
            Run("interrupted restore journal replay", JournalReplay);
            Run("offscreen and negative monitor geometry", MonitorGeometry);
            Run("debounce coalesces edits and flush uses latest", Debounce);
            Run("note create edit duplicate hide show and delete", NoteOperations);
            Run("localized resources embedded", Localization);
            Run("native mode transitions preserve visibility opacity and parent", NativeTransitions);
            Run("single instance IPC activates first instance", InstanceActivation);
            Run("single instance IPC acknowledges activation before success", InstanceAcknowledgment);
            Run("single instance IPC survives a timed-out client", InstanceTimeoutRecovery);
            Run("single instance IPC accepts repeated child processes", InstanceProcesses);
            Run("restore archives corrupt bytes exactly", RestoreCorruptBytes);
            Run("snapshots are detached and dispatcher-affine", DetachedSnapshots);
            Run("startup defaults preserve opt-out and system language", StartupDefaults);
            Run("inline title and note sliders update model", NoteControls);
            Run("RGB picker updates preview and selected color", ColorControls);
            Run("version checks reject malformed responses", VersionChecks);
            Run("settings expose color buttons sliders and version without language picker", SettingsControls);
            Console.WriteLine("Passed: " + passed + ", Failed: " + failures); return failures == 0 ? 0 : 1;
        }
        // QA: UTF-8 JSON round trip and UTC.
        private static void JsonRoundTrip()
        {
            var data = Data("한글\nsecond line"); var copy = StorageService.ReadBackup(Json.Write(data));
            Equal(data.notes.notes[0].content, copy.notes.notes[0].content); Equal(data.notes.notes[0].id, copy.notes.notes[0].id);
        }

        // QA: schema migration boundary rejects unknown and missing versions.
        private static void SchemaBoundary()
        {
            Throws(() => Json.Read<NoteDocument>("{\"schemaVersion\":2,\"notes\":[]}"));
            Throws(() => Json.Read<NoteDocument>("{\"notes\":[]}"));
            Throws(() => Json.Read<NoteDocument>("{\"schemaVersion\":1,\"notes\":[{}]}"));
            Validation.Notes(Json.Read<NoteDocument>("{\"schemaVersion\":1,\"notes\":[]}"));
        }

        // QA: invalid settings and note fields rejected.
        private static void InvalidFields()
        {
            Throws(() => Validation.Settings(new Settings { opacity = double.NaN }));
            Throws(() => Validation.Settings(new Settings { backgroundColor = "red" }));
            Throws(() => Validation.Settings(new Settings { displayMode = "invalid" }));
            var data = Data("x"); data.notes.notes.Add(data.notes.notes[0].Clone()); Throws(() => Validation.Notes(data.notes));
            data = Data("x"); data.notes.notes[0].width = -1; Throws(() => Validation.Notes(data.notes));
        }

        // QA: atomic save retains previous-good data.
        private static void AtomicSave()
        {
            using (var s = Store("atomic"))
            {
                s.SaveAsync(Data("old"), 1).Wait(); s.SaveAsync(Data("new"), 2).Wait();
                Equal("new", s.Load().notes.notes[0].content);
                Equal("old", Json.Read<NoteDocument>(File.ReadAllText(Path.Combine(s.Root, "notes.json.bak"))).notes[0].content);
                Equal(0, Directory.GetFiles(s.Root, "*.tmp").Length);
            }
        }

        // QA: failed replace leaves original intact.
        private static void FailedReplace()
        {
            using (var s = Store("locked"))
            {
                s.SaveAsync(Data("safe"), 1).Wait(); var path = Path.Combine(s.Root, "notes.json");
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) Throws(() => s.SaveAsync(Data("lost"), 2).Wait());
                Equal("safe", s.Load().notes.notes[0].content); Equal(0, Directory.GetFiles(s.Root, "*.tmp").Length);
            }
        }

        // QA: corruption recovers backup and archives original.
        private static void CorruptionRecovery()
        {
            using (var s = Store("corrupt"))
            {
                s.SaveAsync(Data("previous"), 1).Wait(); s.SaveAsync(Data("latest"), 2).Wait();
                File.WriteAllText(Path.Combine(s.Root, "notes.json"), "broken");
                Equal("previous", s.Load().notes.notes[0].content); Equal(true, s.Recovered); Equal(false, s.RecoveryBlocked);
                Equal(1, Directory.GetFiles(s.Root, "*.corrupt-*").Length);
                Equal("previous", Json.Read<NoteDocument>(File.ReadAllText(Path.Combine(s.Root, "notes.json.bak"))).notes[0].content);
            }
        }

        // QA: unrecoverable and future schemas block writes.
        private static void BlockedRecovery()
        {
            using (var s = Store("blocked"))
            {
                var path = Path.Combine(s.Root, "notes.json"); File.WriteAllText(path, "broken"); s.Load();
                Equal(true, s.RecoveryBlocked); Throws(() => s.SaveAsync(Data("overwrite"), 1).Wait()); Equal("broken", File.ReadAllText(path));
            }
            using (var s = Store("future"))
            {
                s.SaveAsync(Data("old"), 1).Wait(); s.SaveAsync(Data("old2"), 2).Wait();
                File.WriteAllText(Path.Combine(s.Root, "notes.json"), "{\"schemaVersion\":99,\"notes\":[]}"); s.Load(); Equal(true, s.RecoveryBlocked);
            }
        }

        // QA: concurrent revisions cannot overwrite latest snapshot.
        private static void ConcurrentRevisions()
        {
            using (var s = Store("concurrent"))
            {
                var tasks = Enumerable.Range(1, 40).Reverse().Select(i => s.SaveAsync(Data(i.ToString()), i)).ToArray(); Task.WaitAll(tasks);
                Equal("40", s.Load().notes.notes[0].content);
            }
        }

        // QA: backup export import validation and restore transaction.
        private static void BackupRestore()
        {
            using (var s = Store("restore"))
            {
                var old = Data("before"); s.SaveAsync(old, 1).Wait(); var backups = new BackupService(s);
                var path = Path.Combine(s.Root, "export.json"); backups.Export(path, Data("after"));
                s.RestoreAsync(backups.Import(path), old, 2).Wait(); Equal("after", s.Load().notes.notes[0].content);
                Equal(1, Directory.GetFiles(Path.Combine(s.Root, "backups"), "*.json").Length);
                File.WriteAllText(path, "bad"); Throws(() => backups.Import(path)); Equal("after", s.Load().notes.notes[0].content);
                Throws(() => backups.Export(Path.Combine(s.Root, "notes.json"), old));
                var fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                backups = new BackupService(s, () => fixedTime);
                backups.AutomaticBackup(old);
                backups.AutomaticBackup(old);
                Equal(3, Directory.GetFiles(Path.Combine(s.Root, "backups"), "*.json").Length);
                for (int i = 0; i < 13; i++) backups.AutomaticBackup(old);
                Equal(10, Directory.GetFiles(Path.Combine(s.Root, "backups"), "*.json").Length);
            }
        }

        // QA: interrupted restore journal replay.
        private static void JournalReplay()
        {
            using (var s = Store("journal"))
            {
                s.SaveAsync(Data("old"), 1).Wait(); AtomicFile.Write(Path.Combine(s.Root, "restore.pending.json"), Json.Write(Data("replayed")));
                Equal("replayed", s.Load().notes.notes[0].content); Equal(false, File.Exists(Path.Combine(s.Root, "restore.pending.json")));
            }
        }

        // QA: offscreen and negative monitor geometry.
        private static void MonitorGeometry()
        {
            var screens = new[] { new Rect(-1920, 0, 1920, 1040), new Rect(0, 0, 1920, 1040) };
            Equal(new Rect(-1800, 100, 250, 300), ScreenService.Clamp(new Rect(-1800, 100, 250, 300), screens));
            var result = ScreenService.Clamp(new Rect(9000, -5000, 500, 400), screens); Equal(true, screens.Any(r => r.Contains(result)));
            result = ScreenService.Clamp(new Rect(100, 100, 9000, 9000), screens); Equal(true, screens.Any(r => r.Contains(result)));
        }

        // QA: debounce coalesces edits and flush uses latest.
        private static void Debounce()
        {
            using (var s = Store("debounce"))
            {
                int snapshots = 0; var data = Data("first"); Exception failure = null;
                using (var autosave = new AutosaveService(s, () => { snapshots++; return Json.Copy(data); }, e => failure = e, 80))
                {
                    for (int i = 0; i < 20; i++) { data.notes.notes[0].content = i.ToString(); autosave.Request(); }
                    Pump(350); Equal(1, snapshots); Equal(null, failure); Equal("19", s.Load().notes.notes[0].content);
                    data.notes.notes[0].content = "flush"; autosave.Request(); var task = autosave.FlushAsync(); Await(task); Equal("flush", s.Load().notes.notes[0].content);
                }
            }
        }

        // QA: note create edit duplicate hide show delete and manager search.
        private static void NoteOperations()
        {
            int changes = 0; var log = new LogService(Path.Combine(root, "ui"));
            using (var service = new NoteService(new Settings { displayMode = "normal" }, () => changes++, log))
            {
                var n = service.Create(); n.content = "searchable"; var copy = service.Create(n);
                Equal(false, n.id == copy.id); Equal(n.content, copy.content); service.Hide(n); Equal(false, n.isVisible); service.Show(n); Equal(true, n.isVisible);
                var manager = new StickyNote.ViewModels.NoteManagerViewModel(service);
                manager.Search = "searchable"; Equal(2, manager.Notes.Cast<Note>().Count());
                manager.Search = "absent"; Equal(0, manager.Notes.Cast<Note>().Count());
                service.Delete(copy); Equal(1, service.Notes.Count); Equal(true, changes > 4);
            }
        }

        // QA: localized resources embedded.
        private static void Localization()
        {
            Loc.Culture = System.Globalization.CultureInfo.GetCultureInfo("en"); Equal("New note", Loc.Get("NewNote"));
            Loc.Culture = System.Globalization.CultureInfo.GetCultureInfo("ko"); Equal("새 메모", Loc.Get("NewNote"));
        }

        // QA: native mode transitions preserve visibility opacity and parent.
        private static void NativeTransitions()
        {
            var log = new LogService(Path.Combine(root, "modes"));
            using (var service = new NoteService(new Settings(), () => { }, log))
            {
                var note = new Note { displayMode = "normal" };
                var window = new StickyNote.Views.NoteWindow(note, service, log);
                try
                {
                    window.Show(); Pump(30);
                    var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    var parent = StickyNote.Interop.NativeMethods.GetParent(handle);
                    note.opacity = .7; note.displayMode = "topmost"; Equal(true, window.Topmost);
                    Equal(true, StickyNote.Interop.NativeMethods.IsWindowVisible(handle));
                    note.displayMode = "desktop"; Equal("desktop", note.displayMode);
                    Equal(parent, StickyNote.Interop.NativeMethods.GetParent(handle));
                    note.displayMode = "normal"; Equal(false, window.Topmost); Equal(.7, window.Opacity);
                    Equal(true, StickyNote.Interop.NativeMethods.IsWindowVisible(handle));
                }
                finally { window.Destroy(); }
            }
        }

        // QA: single instance IPC activates first instance.
        private static void InstanceActivation()
        {
            var scope = Guid.NewGuid().ToString("N");
            using (var first = new SingleInstanceService(scope))
            {
                Equal(true, first.IsFirst);
                using (var activated = new ManualResetEvent(false))
                {
                    first.Listen(() => activated.Set(), new LogService(Path.Combine(root, "ipc")));
                    using (var second = new SingleInstanceService(scope))
                    {
                        Equal(false, second.IsFirst); Equal(true, second.Signal()); Equal(true, activated.WaitOne(3000));
                    }
                }
            }
        }
        // QA: success must wait for the callback, even when it is delayed.
        private static void InstanceAcknowledgment()
        {
            var scope = Guid.NewGuid().ToString("N");
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var first = new SingleInstanceService(scope))
            using (var second = new SingleInstanceService(scope))
            {
                first.Listen(() => { entered.Set(); release.WaitOne(5000); }, new LogService(Path.Combine(root, "ipc-ack")));
                var signal = Task.Run(() => second.Signal());
                try
                {
                    Equal(true, entered.WaitOne(3000));
                    Equal(false, signal.IsCompleted);
                }
                finally { release.Set(); signal.Wait(5000); }
                Equal(true, signal.Result);
            }
        }
        // QA: exercise the real process boundary and listener re-arming.
        private static void InstanceProcesses()
        {
            var scope = Guid.NewGuid().ToString("N");
            using (var first = new SingleInstanceService(scope))
            {
                int activations = 0;
                first.Listen(() => Interlocked.Increment(ref activations), new LogService(Path.Combine(root, "ipc-process")));
                for (int i = 1; i <= 10; i++)
                {
                    RunSignalProcess(scope);
                    Equal(i, Interlocked.CompareExchange(ref activations, 0, 0));
                }
            }
        }
        // QA: a client that leaves before acknowledgment cannot disable IPC.
        private static void InstanceTimeoutRecovery()
        {
            var scope = Guid.NewGuid().ToString("N");
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var first = new SingleInstanceService(scope))
            using (var second = new SingleInstanceService(scope))
            {
                first.Listen(() => { entered.Set(); release.WaitOne(10000); }, new LogService(Path.Combine(root, "ipc-timeout")));
                var signal = Task.Run(() => second.Signal());
                try
                {
                    Equal(true, entered.WaitOne(3000));
                    Equal(true, signal.Wait(5000));
                    Equal(false, signal.Result);
                }
                finally { release.Set(); signal.Wait(5000); }
                Equal(true, second.Signal());
            }
        }
        // QA: only the isolated test child can be terminated on timeout.
        private static void RunSignalProcess(string scope)
        {
            var start = new System.Diagnostics.ProcessStartInfo(typeof(Program).Assembly.Location, "--signal-instance " + scope);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            using (var child = System.Diagnostics.Process.Start(start))
            {
                if (!child.WaitForExit(10000))
                {
                    child.Kill(); child.WaitForExit();
                    throw new TimeoutException("IPC test child did not exit.");
                }
                Equal(0, child.ExitCode);
            }
        }
        // QA: secondary process uses the same user/session-scoped IPC path.
        private static int SignalInstance(string scope)
        {
            using (var second = new SingleInstanceService(scope))
            {
                if (second.IsFirst) return 2;
                return second.Signal() ? 0 : 3;
            }
        }
        // QA: restore must not transcode corrupt originals.
        private static void RestoreCorruptBytes()
        {
            using (var storage = Store("binary-corruption"))
            {
                var path = Path.Combine(storage.Root, "notes.json");
                var corrupt = new byte[] { 0xff, 0xfe, 0x00, 0xd8, 0x81 };
                File.WriteAllBytes(path, corrupt);
                storage.Load();
                Equal(true, storage.RecoveryBlocked);
                storage.RestoreAsync(Data("restored"), Data("current"), 1).Wait();
                var archived = Directory.GetFiles(storage.Root, "notes.json.before-restore-*").Single();
                Equal(true, corrupt.SequenceEqual(File.ReadAllBytes(archived)));
                Equal("restored", storage.Load().notes.notes[0].content);
            }
        }
        // QA: clones retain data without sharing events or UI state.
        private static void DetachedSnapshots()
        {
            int changes = 0;
            using (var service = new NoteService(new Settings(), () => changes++, new LogService(Path.Combine(root, "snapshot"))))
            {
                var note = new Note { content = "source", isVisible = false, monitorDeviceName = "monitor" };
                service.Load(new[] { note });
                var snapshot = service.Snapshot();
                var copy = snapshot.notes.notes.Single();
                Equal(Json.Write(note), Json.Write(copy));
                copy.content = "copy";
                snapshot.settings.fontSize = 24;
                Equal("source", note.content);
                Equal(14d, service.Settings.fontSize);
                Equal(0, changes);
                Throws(() => Task.Run(() => service.Snapshot()).GetAwaiter().GetResult());
            }
        }
        // QA: new defaults must not overwrite a persisted opt-out.
        private static void StartupDefaults()
        {
            Equal(true, new Settings().startWithWindows);
            var optedOut = Json.Read<Settings>(Json.Write(new Settings { startWithWindows = false }));
            Equal(false, optedOut.startWithWindows);
            var previous = Loc.Culture;
            try
            {
                Loc.Culture = System.Globalization.CultureInfo.GetCultureInfo("ko-KR");
                Equal("새 메모", Loc.Get("NewNote"));
                Loc.Culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
                Equal("New note", Loc.Get("NewNote"));
                Loc.Culture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
                Equal("New note", Loc.Get("NewNote"));
            }
            finally { Loc.Culture = previous; }
        }
        // QA: inline controls bind both ways and survive snapshot capture.
        private static void NoteControls()
        {
            using (var service = new NoteService(new Settings { displayMode = "normal" }, () => { }, new LogService(Path.Combine(root, "controls"))))
            {
                var note = new Note { title = "Before", displayMode = "normal", isVisible = false };
                service.Load(new[] { note });
                TestNoteWindow(service, note);
            }
        }
        // QA: interact with WPF controls, including removed status/menu labels.
        private static void TestNoteWindow(NoteService service, Note note)
        {
            var window = new StickyNote.Views.NoteWindow(note, service, new LogService(Path.Combine(root, "control-window")));
            try
            {
                var textStyle = new Style(typeof(System.Windows.Controls.TextBox));
                textStyle.Setters.Add(new Setter(System.Windows.Controls.Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
                window.Resources.Add(typeof(System.Windows.Controls.TextBox), textStyle);
                window.Show(); Pump(30);
                var editor = (System.Windows.Controls.TextBox)window.FindName("Editor");
                var title = (System.Windows.Controls.TextBox)window.FindName("TitleBox");
                Equal(VerticalAlignment.Top, editor.VerticalContentAlignment);
                Equal(TextAlignment.Left, editor.TextAlignment);
                editor.Text = "왼쪽 위에서 입력합니다.\n두 번째 줄입니다.";
                title.Text = "After";
                ((System.Windows.Controls.Slider)window.FindName("OpacityGauge")).Value = .55;
                ((System.Windows.Controls.Slider)window.FindName("FontGauge")).Value = 24;
                Pump(30);
                Equal("After", note.title); Equal(.55, note.opacity); Equal(24d, note.fontSize);
                Equal(null, window.FindName("ModeStatus"));
                var headers = window.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().Select(item => item.Header).ToArray();
                Equal(true, headers.Contains(Loc.Get("Settings")));
                Equal(true, headers.Contains(Loc.Get("Manager")));
                Equal(false, headers.Contains("Rename"));
                Equal("After", service.Snapshot().notes.notes.Single().title);
                SavePreview(window, "note-preview.png");
            }
            finally { window.Destroy(); }
        }
        // QA: integer channel sliders produce the matching HEX selection.
        private static void ColorControls()
        {
            var dialog = new StickyNote.Views.ColorWindow("#123456");
            try
            {
                Equal("#123456", dialog.SelectedColor);
                var sliders = ((System.Windows.Controls.StackPanel)dialog.Content).Children.OfType<System.Windows.Controls.Slider>().ToArray();
                Equal(3, sliders.Length);
                sliders[0].Value = 255; sliders[1].Value = 0; sliders[2].Value = 128;
                Equal("#FF0080", dialog.SelectedColor);
                dialog.Show(); Pump(30);
                SavePreview(dialog, "color-preview.png");
            }
            finally { dialog.Close(); }
        }
        // QA: compare version numbers; never accept arbitrary response content.
        private static void VersionChecks()
        {
            Equal(new Version(1, 2, 3, 0), UpdateService.ParseVersion("1.2.3"));
            Equal(File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "VERSION")).Trim(), UpdateService.Current.ToString(3));
            Throws(() => UpdateService.ParseVersion("<html>error</html>"));
            Throws(() => UpdateService.ParseVersion("1.2"));
            Throws(() => UpdateService.ParseVersion("1.2.3.4"));
            Throws(() => UpdateService.ParseVersion(null));
        }
        // QA: render isolated test windows without capturing the user's desktop.
        private static void SavePreview(Window window, string name)
        {
            var content = (FrameworkElement)window.Content;
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var drawing = new System.Windows.Media.DrawingVisual();
            using (var context = drawing.RenderOpen())
            {
                var bounds = new Rect(0, 0, content.ActualWidth, content.ActualHeight);
                context.DrawRectangle(window.Background ?? System.Windows.Media.Brushes.White, null, bounds);
                context.DrawRectangle(new System.Windows.Media.VisualBrush(content), null, bounds);
            }
            bitmap.Render(drawing);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            var path = Path.Combine(root, name);
            using (var stream = new FileStream(path + ".tmp", FileMode.CreateNew)) { encoder.Save(stream); stream.Flush(true); }
            File.Move(path + ".tmp", path);
        }
        // QA: load real app styles without starting storage, tray or startup registration.
        private static void SettingsControls()
        {
            var app = new App(); app.InitializeComponent();
            using (var service = new NoteService(new Settings(), () => { }, new LogService(Path.Combine(root, "settings-ui"))))
            {
                typeof(App).GetProperty("Notes").GetSetMethod(true).Invoke(app, new object[] { service });
                var window = new StickyNote.Views.SettingsWindow(app);
                try
                {
                    window.Show(); Pump(30);
                    Equal(true, ((System.Windows.Controls.CheckBox)window.FindName("Startup")).IsChecked);
                    Equal(null, window.FindName("LanguageChoice"));
                    Equal(null, window.FindName("BackgroundChoice"));
                    Equal(true, window.FindName("BackgroundButton") is System.Windows.Controls.Button);
                    Equal(true, window.FindName("ForegroundButton") is System.Windows.Controls.Button);
                    Equal(true, window.FindName("FontInput") is System.Windows.Controls.Slider);
                    Equal(UpdateService.Current.ToString(3), ((System.Windows.Controls.TextBlock)window.FindName("VersionValue")).Text);
                    SavePreview(window, "settings-preview.png");
                }
                finally { window.Close(); }
            }
        }
        // QA: isolated storage per case.
        private static StorageService Store(string name)
        {
            var path = Path.Combine(root, name);
            return new StorageService(path, new LogService(path));
        }
        // QA: valid seed document.
        private static BackupDocument Data(string text) { return new BackupDocument { notes = new NoteDocument { notes = new System.Collections.Generic.List<Note> { new Note { title = "test", content = text } } }, settings = new Settings() }; }
        // QA: assert expected value.
        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
        }
        // QA: assert rejection.
        private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection."); }
        // QA: report one case result.
        private static void Run(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e); } }
        // QA: bounded dispatcher-aware wait.
        private static void Await(Task task)
        {
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!task.IsCompleted && DateTime.UtcNow < until) Pump(20);
            if (!task.IsCompleted) throw new TimeoutException();
            task.GetAwaiter().GetResult();
        }
        // QA: advance dispatcher timers.
        private static void Pump(int ms) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; timer.Tick += (s, e) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    }
}
