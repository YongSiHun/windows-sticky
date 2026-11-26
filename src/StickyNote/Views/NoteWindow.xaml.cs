using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StickyNote.Models;
using StickyNote.Services;
using StickyNote.Utilities;
using Validation = StickyNote.Utilities.Validation;

namespace StickyNote.Views
{
    public partial class NoteWindow : Window
    {
        private readonly Note note;
        private readonly NoteService service;
        private readonly LogService log;
        private DesktopWidgetService desktop;
        private bool destroying, ready;
        // INIT: wire owned state and events.
        public NoteWindow(Note note, NoteService service, LogService log)
        {
            this.note = note; this.service = service; this.log = log;
            InitializeComponent(); DataContext = note;
            Left = note.x; Top = note.y; Width = note.width; Height = note.height;
            AddButton.ToolTip = Loc.Get("NewNote"); HideButton.ToolTip = Loc.Get("Hide"); MenuButton.ToolTip = Loc.Get("Options");
            System.Windows.Automation.AutomationProperties.SetName(AddButton, Loc.Get("NewNote"));
            System.Windows.Automation.AutomationProperties.SetName(HideButton, Loc.Get("Hide"));
            System.Windows.Automation.AutomationProperties.SetName(TitleBox, Loc.Get("Title"));
            System.Windows.Automation.AutomationProperties.SetName(Editor, Loc.Get("Content"));
            SourceInitialized += InitializedSource; LocationChanged += GeometryChanged; SizeChanged += GeometryChanged;
            Closing += OnClosing; note.PropertyChanged += NoteChanged;
            ContextMenu = BuildMenu();
        }
        // HWND: initialize display mode after handle creation.
        private void InitializedSource(object sender, EventArgs e)
        {
            Reposition(); ready = true; CaptureGeometry();
            try { desktop = new DesktopWidgetService(this, log); desktop.Changed += ModeChanged; desktop.Apply(note.displayMode); }
            catch (Exception error) { log.Write("desktop-initialize", error); Topmost = note.displayMode == "topmost"; ModeStatus.Text = Loc.Get(Topmost ? "topmost" : "normal") + (note.displayMode == "desktop" ? " · " + Loc.Get("Fallback") : ""); }
        }
        // DPI: clamp to current work area.
        public void Reposition()
        {
            ScreenService.Reposition(this, note);
            if (ready) CaptureGeometry();
        }
        // MODE: show actual mode and fallback.
        private void ModeChanged(object sender, EventArgs e)
        {
            ModeStatus.Text = Loc.Get(desktop.RequestedMode) + (desktop.ActualMode != desktop.RequestedMode ? " → " + Loc.Get(desktop.ActualMode) + " · " + Loc.Get("Fallback") : "");
        }
        // MODE: apply changed request.
        private void NoteChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "displayMode" || desktop == null) return;
            desktop.Apply(note.displayMode);
        }
        // RECT: capture normal window bounds.
        private void GeometryChanged(object sender, EventArgs e)
        {
            if (ready && WindowState == WindowState.Normal) CaptureGeometry();
        }
        // RECT: persist DIP bounds and monitor.
        private void CaptureGeometry()
        {
            note.x = Left;
            note.y = Top;
            note.width = Width;
            note.height = Height;
            ScreenService.UpdateMonitor(this, note);
        }
        // UI: drag title area.
        private void DragTitle(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBox || e.OriginalSource is Button) return;
            if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed) { DragMove(); CaptureGeometry(); }
        }
        // UI: add localized action.
        private void Add(object sender, RoutedEventArgs e)
        {
            service.Create();
        }
        // UI: hide current note.
        private void HideNote(object sender, RoutedEventArgs e)
        {
            service.Hide(note);
        }
        // UI: rebuild current note options.
        private void OpenMenu(object sender, RoutedEventArgs e)
        {
            ContextMenu = BuildMenu();
            ContextMenu.PlacementTarget = MenuButton;
            ContextMenu.IsOpen = true;
        }
        // UI: bind localized menu action.
        private MenuItem Item(string key, Action action)
        {
            var item = new MenuItem { Header = Loc.Get(key) }; item.Click += (s, e) => action(); return item;
        }
        // UI: assemble note commands.
        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(Item("NewNote", () => service.Create())); menu.Items.Add(Item("Duplicate", () => service.Create(note)));
            menu.Items.Add(Item("Rename", Rename));
            menu.Items.Add(Item("Manager", service.Manage)); menu.Items.Add(new Separator());
            menu.Items.Add(BackgroundMenu());
            menu.Items.Add(Item("Foreground", () => ChangeColor(note.foregroundColor, value => note.foregroundColor = value)));
            menu.Items.Add(NumberMenu("FontSize", new[] { 10d, 12, 14, 16, 18, 24, 32, 48, 72 }, "G", value => note.fontSize = value));
            menu.Items.Add(NumberMenu("Opacity", new[] { .3, .5, .7, .85, 1 }, "P0", value => note.opacity = value));
            var modes = ModeMenu();
            menu.Items.Add(modes); menu.Items.Add(new Separator()); menu.Items.Add(Item("Hide", () => service.Hide(note)));
            menu.Items.Add(Item("Delete", Delete));
            return menu;
        }
        // TITLE: cancel leaves note unchanged.
        private void Rename()
        {
            var value = InputWindow.Ask(this, "Title", note.title, 500);
            if (value != null) note.title = value;
        }
        // RGB: shared color editor.
        private void ChangeColor(string current, Action<string> apply)
        {
            var value = InputWindow.Ask(this, "ColorHint", current);
            if (value == null) return;
            if (!Validation.Color(value)) { MessageBox.Show(this, Loc.Get("Invalid")); return; }
            apply(value);
        }
        // BG: preset palette.
        private MenuItem BackgroundMenu()
        {
            var colors = new MenuItem { Header = Loc.Get("Background") };
            for (int i = 0; i < Validation.Colors.Length; i++)
            {
                var color = Validation.Colors[i];
                var item = Item("Color" + i, () => note.backgroundColor = color);
                item.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color);
                colors.Items.Add(item);
            }
            colors.Items.Add(Item("Custom", () => ChangeColor(note.backgroundColor, value => note.backgroundColor = value)));
            return colors;
        }
        // NUM: shared numeric options.
        private MenuItem NumberMenu(string key, double[] values, string format, Action<double> apply)
        {
            var menu = new MenuItem { Header = Loc.Get(key) };
            foreach (var value in values)
            {
                var item = new MenuItem { Header = value.ToString(format) };
                item.Click += (s, e) => apply(value);
                menu.Items.Add(item);
            }
            return menu;
        }
        // MODE: requested mode stays checked after fallback.
        private MenuItem ModeMenu()
        {
            var menu = new MenuItem { Header = Loc.Get("Mode") };
            foreach (var mode in new[] { "desktop", "normal", "topmost" })
            {
                var item = Item(mode, () => note.displayMode = mode);
                item.IsCheckable = true;
                item.IsChecked = note.displayMode == mode;
                menu.Items.Add(item);
            }
            return menu;
        }
        // DEL: explicit confirmation.
        private void Delete()
        {
            if (MessageBox.Show(this, Loc.Get("DeleteConfirm"), Loc.Get("AppTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            service.Delete(note);
        }
        // UI: hide note on user close.
        private void OnClosing(object sender, CancelEventArgs e) { if (!destroying) { e.Cancel = true; Dispatcher.BeginInvoke(new Action(() => service.Hide(note))); } }
        // FREE: detach events and destroy HWND.
        public void Destroy()
        {
            if (destroying) return; destroying = true; ready = false;
            note.PropertyChanged -= NoteChanged;
            if (desktop != null) { desktop.Changed -= ModeChanged; desktop.Dispose(); }
            SourceInitialized -= InitializedSource; LocationChanged -= GeometryChanged; SizeChanged -= GeometryChanged; Closing -= OnClosing;
            Close();
        }
    }
    public sealed class InputWindow : Window
    {
        // INIT: wire owned state and events.
        private InputWindow() { }
        // UI: bounded text input.
        public static string Ask(Window owner, string key, string value, int maximumLength = 7)
        {
            var dialog = new InputWindow { Owner = owner, Title = Loc.Get(key), Width = 320, Height = 150, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
            var panel = new StackPanel { Margin = new Thickness(12) }; var input = new TextBox { Text = value, MaxLength = maximumLength };
            var ok = new Button { Content = Loc.Get("Save"), IsDefault = true }; ok.Click += (s, e) => dialog.DialogResult = true;
            panel.Children.Add(input); panel.Children.Add(ok); dialog.Content = panel;
            dialog.Loaded += (s, e) => { input.Focus(); input.SelectAll(); };
            return dialog.ShowDialog() == true ? input.Text : null;
        }
    }
}
