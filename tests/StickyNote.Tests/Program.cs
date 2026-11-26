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
        // QA: run independent regression cases.
        [STAThread]
        private static int Main()
        {
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
            Run("note create edit duplicate hide show delete and manager search", NoteOperations);
            Run("localized resources embedded", Localization);
            Run("native mode transitions preserve visibility opacity and parent", NativeTransitions);
            Run("single instance IPC activates first instance", InstanceActivation);
            Run("restore archives corrupt bytes exactly", RestoreCorruptBytes);
            Run("snapshots are detached and dispatcher-affine", DetachedSnapshots);
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
                var vm = new StickyNote.ViewModels.NoteManagerViewModel(service); vm.Search = "searchable"; Equal(2, vm.Notes.Cast<Note>().Count());
                vm.Search = "absent"; Equal(0, vm.Notes.Cast<Note>().Count()); service.Delete(copy); Equal(1, service.Notes.Count); Equal(true, changes > 4);
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
