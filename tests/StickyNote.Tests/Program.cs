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

namespace StickyNote.Tests
{
    internal static class Program
    {
        private static int failures, passed;
        private static string root;
        [STAThread]
        private static int Main()
        {
            root = Path.Combine(Environment.CurrentDirectory, "TestResults", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Run("UTF-8 JSON round trip and UTC", () =>
            {
                var data = Data("한글\nsecond line"); var copy = StorageService.ReadBackup(Json.Write(data));
                Equal(data.notes.notes[0].content, copy.notes.notes[0].content); Equal(data.notes.notes[0].id, copy.notes.notes[0].id);
            });
            Run("schema migration boundary rejects unknown and missing versions", () =>
            {
                Throws(() => Json.Read<NoteDocument>("{\"schemaVersion\":2,\"notes\":[]}"));
                Throws(() => Json.Read<NoteDocument>("{\"notes\":[]}"));
                Throws(() => Json.Read<NoteDocument>("{\"schemaVersion\":1,\"notes\":[{}]}"));
                Validation.Notes(Json.Read<NoteDocument>("{\"schemaVersion\":1,\"notes\":[]}"));
            });
            Run("invalid settings and note fields rejected", () =>
            {
                Throws(() => Validation.Settings(new Settings { opacity = double.NaN }));
                Throws(() => Validation.Settings(new Settings { backgroundColor = "red" }));
                Throws(() => Validation.Settings(new Settings { displayMode = "invalid" }));
                var data = Data("x"); data.notes.notes.Add(data.notes.notes[0].Clone()); Throws(() => Validation.Notes(data.notes));
                data = Data("x"); data.notes.notes[0].width = -1; Throws(() => Validation.Notes(data.notes));
            });
            Run("atomic save retains previous-good data", () =>
            {
                using (var s = Store("atomic"))
                {
                    s.SaveAsync(Data("old"), 1).Wait(); s.SaveAsync(Data("new"), 2).Wait();
                    Equal("new", s.Load().notes.notes[0].content);
                    Equal("old", Json.Read<NoteDocument>(File.ReadAllText(Path.Combine(s.Root, "notes.json.bak"))).notes[0].content);
                    Equal(0, Directory.GetFiles(s.Root, "*.tmp").Length);
                }
            });
            Run("failed replace leaves original intact", () =>
            {
                using (var s = Store("locked"))
                {
                    s.SaveAsync(Data("safe"), 1).Wait(); var path = Path.Combine(s.Root, "notes.json");
                    using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) Throws(() => s.SaveAsync(Data("lost"), 2).Wait());
                    Equal("safe", s.Load().notes.notes[0].content); Equal(0, Directory.GetFiles(s.Root, "*.tmp").Length);
                }
            });
            Run("corruption recovers backup and archives original", () =>
            {
                using (var s = Store("corrupt"))
                {
                    s.SaveAsync(Data("previous"), 1).Wait(); s.SaveAsync(Data("latest"), 2).Wait();
                    File.WriteAllText(Path.Combine(s.Root, "notes.json"), "broken");
                    Equal("previous", s.Load().notes.notes[0].content); Equal(true, s.Recovered); Equal(false, s.RecoveryBlocked);
                    Equal(1, Directory.GetFiles(s.Root, "*.corrupt-*").Length);
                    Equal("previous", Json.Read<NoteDocument>(File.ReadAllText(Path.Combine(s.Root, "notes.json.bak"))).notes[0].content);
                }
            });
            Run("unrecoverable and future schemas block writes", () =>
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
            });
            Run("concurrent revisions cannot overwrite latest snapshot", () =>
            {
                using (var s = Store("concurrent"))
                {
                    var tasks = Enumerable.Range(1, 40).Reverse().Select(i => s.SaveAsync(Data(i.ToString()), i)).ToArray(); Task.WaitAll(tasks);
                    Equal("40", s.Load().notes.notes[0].content);
                }
            });
            Run("backup export import validation and restore transaction", () =>
            {
                using (var s = Store("restore"))
                {
                    var old = Data("before"); s.SaveAsync(old, 1).Wait(); var backups = new BackupService(s);
                    var path = Path.Combine(s.Root, "export.json"); backups.Export(path, Data("after"));
                    s.RestoreAsync(backups.Import(path), old, 2).Wait(); Equal("after", s.Load().notes.notes[0].content);
                    Equal(1, Directory.GetFiles(Path.Combine(s.Root, "backups"), "*.json").Length);
                    File.WriteAllText(path, "bad"); Throws(() => backups.Import(path)); Equal("after", s.Load().notes.notes[0].content);
                    Throws(() => backups.Export(Path.Combine(s.Root, "notes.json"), old));
                    for (int i = 0; i < 13; i++) backups.AutomaticBackup(old);
                    Equal(10, Directory.GetFiles(Path.Combine(s.Root, "backups"), "*.json").Length);
                }
            });
            Run("interrupted restore journal replay", () =>
            {
                using (var s = Store("journal"))
                {
                    s.SaveAsync(Data("old"), 1).Wait(); StorageService.AtomicWrite(Path.Combine(s.Root, "restore.pending.json"), Json.Write(Data("replayed")));
                    Equal("replayed", s.Load().notes.notes[0].content); Equal(false, File.Exists(Path.Combine(s.Root, "restore.pending.json")));
                }
            });
            Run("offscreen and negative monitor geometry", () =>
            {
                var screens = new[] { new Rect(-1920, 0, 1920, 1040), new Rect(0, 0, 1920, 1040) };
                Equal(new Rect(-1800, 100, 250, 300), ScreenService.Clamp(new Rect(-1800, 100, 250, 300), screens));
                var result = ScreenService.Clamp(new Rect(9000, -5000, 500, 400), screens); Equal(true, screens.Any(r => r.Contains(result)));
                result = ScreenService.Clamp(new Rect(100, 100, 9000, 9000), screens); Equal(true, screens.Any(r => r.Contains(result)));
            });
            Run("debounce coalesces edits and flush uses latest", () =>
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
            });
            Run("note create edit duplicate hide show delete and manager search", () =>
            {
                int changes = 0; var log = new LogService(Path.Combine(root, "ui"));
                using (var service = new NoteService(new Settings { displayMode = "normal" }, () => changes++, log))
                {
                    var n = service.Create(); n.content = "searchable"; var copy = service.Create(n);
                    Equal(false, n.id == copy.id); Equal(n.content, copy.content); service.Hide(n); Equal(false, n.isVisible); service.Show(n); Equal(true, n.isVisible);
                    var vm = new StickyNote.ViewModels.NoteManagerViewModel(service); vm.Search = "searchable"; Equal(2, vm.Notes.Cast<Note>().Count());
                    vm.Search = "absent"; Equal(0, vm.Notes.Cast<Note>().Count()); service.Delete(copy); Equal(1, service.Notes.Count); Equal(true, changes > 4);
                }
            });
            Run("localized resources embedded", () =>
            {
                Loc.Culture = System.Globalization.CultureInfo.GetCultureInfo("en"); Equal("New note", Loc.Get("NewNote"));
                Loc.Culture = System.Globalization.CultureInfo.GetCultureInfo("ko"); Equal("새 메모", Loc.Get("NewNote"));
            });
            Run("native mode transitions preserve visibility opacity and parent", () =>
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
            });
            Run("single instance IPC activates first instance", () =>
            {
                using (var first = new SingleInstanceService())
                {
                    Equal(true, first.IsFirst);
                    using (var activated = new ManualResetEvent(false))
                    {
                        first.Listen(() => activated.Set(), new LogService(Path.Combine(root, "ipc")));
                        using (var second = new SingleInstanceService())
                        {
                            Equal(false, second.IsFirst); Equal(true, second.Signal()); Equal(true, activated.WaitOne(3000));
                        }
                    }
                }
            });
            Console.WriteLine("Passed: " + passed + ", Failed: " + failures); return failures == 0 ? 0 : 1;
        }
        private static StorageService Store(string name) { var path = Path.Combine(root, name); return new StorageService(path, new LogService(path)); }
        private static BackupDocument Data(string text) { return new BackupDocument { notes = new NoteDocument { notes = new System.Collections.Generic.List<Note> { new Note { title = "test", content = text } } }, settings = new Settings() }; }
        private static void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
        private static void Throws(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection."); }
        private static void Run(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e); } }
        private static void Await(Task task) { var until = DateTime.UtcNow.AddSeconds(10); while (!task.IsCompleted && DateTime.UtcNow < until) Pump(20); if (!task.IsCompleted) throw new TimeoutException(); task.GetAwaiter().GetResult(); }
        private static void Pump(int ms) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; timer.Tick += (s, e) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    }
}
