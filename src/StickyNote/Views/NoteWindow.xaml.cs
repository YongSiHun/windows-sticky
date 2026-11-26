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
            System.Windows.Automation.AutomationProperties.SetName(OpacityGauge, Loc.Get("Opacity"));
            System.Windows.Automation.AutomationProperties.SetName(FontGauge, Loc.Get("FontSize"));
            SourceInitialized += InitializedSource; LocationChanged += GeometryChanged; SizeChanged += GeometryChanged;
            Closing += OnClosing; note.PropertyChanged += NoteChanged;
            ContextMenu = BuildMenu();
        }
        // HWND: initialize display mode after handle creation.
        private void InitializedSource(object sender, EventArgs e)
        {
            Reposition(); ready = true; CaptureGeometry();
            try { desktop = new DesktopWidgetService(this, log); desktop.Apply(note.displayMode); }
            catch (Exception error) { log.Write("desktop-initialize", error); Topmost = note.displayMode == "topmost"; }
        }
        // DPI: clamp to current work area.
        public void Reposition()
        {
            ScreenService.Reposition(this, note);
            if (ready) CaptureGeometry();
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
            if (TitleBox.IsMouseOver || AddButton.IsMouseOver || MenuButton.IsMouseOver || HideButton.IsMouseOver) return;
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
            menu.Items.Add(Item("Manager", () => ((App)Application.Current).ShowManager()));
            menu.Items.Add(Item("ShowAll", service.ShowAll));
            menu.Items.Add(Item("Settings", () => ((App)Application.Current).ShowSettings()));
            menu.Items.Add(new Separator());
            menu.Items.Add(BackgroundMenu());
            menu.Items.Add(Item("Foreground", () => ChangeColor(note.foregroundColor, value => note.foregroundColor = value)));
            var modes = ModeMenu();
            menu.Items.Add(modes); menu.Items.Add(new Separator()); menu.Items.Add(Item("Hide", () => service.Hide(note)));
            menu.Items.Add(Item("Delete", Delete));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("Exit", () => ((App)Application.Current).ExitApplication()));
            return menu;
        }
        // RGB: shared color editor.
        private void ChangeColor(string current, Action<string> apply)
        {
            var value = ColorWindow.Ask(this, current);
            if (value == null) return;
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
            if (desktop != null) desktop.Dispose();
            SourceInitialized -= InitializedSource; LocationChanged -= GeometryChanged; SizeChanged -= GeometryChanged; Closing -= OnClosing;
            Close();
        }
    }
}
