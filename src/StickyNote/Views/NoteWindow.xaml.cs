using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StickyNote.Models;
using StickyNote.Services;
using StickyNote.Utilities;

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
            TitleBox.LostKeyboardFocus += (s, e) => FinishTitleEdit();
            TitleBox.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Escape) { FinishTitleEdit(); Editor.Focus(); e.Handled = true; } };
            TitleBox.ToolTip = Loc.Get("EditTitleHint");
            TitleDisplay.ToolTip = Loc.Get("EditTitleHint");
            Left = note.x; Top = note.y; Width = note.width; Height = note.height;
            AddButton.ToolTip = Loc.Get("NewNote"); DeleteButton.ToolTip = Loc.Get("Delete"); MenuButton.ToolTip = Loc.Get("Options");
            RestoreButton.ToolTip = Loc.Get("RestoreSize");
            System.Windows.Automation.AutomationProperties.SetName(RestoreButton, Loc.Get("RestoreSize"));
            OpacityLabel.Text = Loc.Get("Opacity"); FontLabel.Text = Loc.Get("FontSizeShort");
            System.Windows.Automation.AutomationProperties.SetName(AddButton, Loc.Get("NewNote"));
            System.Windows.Automation.AutomationProperties.SetName(DeleteButton, Loc.Get("Delete"));
            System.Windows.Automation.AutomationProperties.SetName(TitleBox, Loc.Get("Title"));
            System.Windows.Automation.AutomationProperties.SetName(Editor, Loc.Get("Content"));
            System.Windows.Automation.AutomationProperties.SetName(OpacityGauge, Loc.Get("Opacity"));
            System.Windows.Automation.AutomationProperties.SetName(FontGauge, Loc.Get("FontSize"));
            SourceInitialized += InitializedSource; LocationChanged += GeometryChanged; SizeChanged += GeometryChanged;
            Closing += OnClosing; note.PropertyChanged += NoteChanged;
            ContextMenu = BuildMenu();
        }
        // UI: raise a normal note and focus its editor without making it always-on-top.
        public void ActivateEditor()
        {
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            Editor.Focus();
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
            if (WindowState != WindowState.Normal) return;
            note.x = Left;
            note.y = Top;
            note.width = Width;
            note.height = Height;
            ScreenService.UpdateMonitor(this, note);
        }
        // UI: drag title area.
        private void DragTitle(object sender, MouseButtonEventArgs e)
        {
            if (!CanDragTitle()) return;
            if (e.ClickCount == 2)
            {
                BeginTitleEdit(); e.Handled = true; return;
            }
            if (e.ClickCount != 1 || e.LeftButton != MouseButtonState.Pressed) return;
            RestoreNoteSize();
            Editor.Focus(); DragMove(); CaptureGeometry(); e.Handled = true;
        }
        // UI: borderless maximized notes need an explicit route back to resizing.
        private void RestoreSize(object sender, RoutedEventArgs e)
        {
            RestoreNoteSize();
        }
        private void RestoreNoteSize()
        {
            if (WindowState == WindowState.Normal) return;
            WindowState = WindowState.Normal;
            CaptureGeometry();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || e.Key != Key.Escape || WindowState != WindowState.Maximized) return;
            RestoreNoteSize(); e.Handled = true;
        }
        private bool CanDragTitle()
        {
            return !(TitleBox.IsMouseOver || AddButton.IsMouseOver || MenuButton.IsMouseOver || RestoreButton.IsMouseOver || DeleteButton.IsMouseOver);
        }
        internal void BeginTitleEdit()
        {
            TitleDisplay.Visibility = Visibility.Collapsed; TitleBox.Visibility = Visibility.Visible;
            TitleBox.Focus(); TitleBox.SelectAll();
        }
        private void EditTitleKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.F2) return;
            BeginTitleEdit(); e.Handled = true;
        }
        private void FinishTitleEdit()
        {
            TitleBox.Visibility = Visibility.Collapsed; TitleDisplay.Visibility = Visibility.Visible;
        }
        // UI: add localized action.
        private void Add(object sender, RoutedEventArgs e)
        {
            service.Create();
        }
        // UI: the X button uses the same confirmation as the delete menu.
        private void DeleteNote(object sender, RoutedEventArgs e)
        {
            Delete();
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
            menu.Items.Add(Item("ShowAll", service.ShowAll));
            menu.Items.Add(Item("RestoreSize", RestoreNoteSize));
            menu.Items.Add(Item("Settings", () => ((App)Application.Current).ShowSettings()));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("Background", () => ChangeColor(note.backgroundColor, new Settings().backgroundColor, value => note.backgroundColor = value)));
            menu.Items.Add(Item("Foreground", () => ChangeColor(note.foregroundColor, new Settings().foregroundColor, value => note.foregroundColor = value)));
            menu.Items.Add(Item("FontFamily", ChangeFont));
            var modes = ModeMenu();
            menu.Items.Add(modes); menu.Items.Add(new Separator());
            menu.Items.Add(Item("Delete", Delete));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("Exit", () => ((App)Application.Current).ExitApplication()));
            return menu;
        }
        // FONT: installed typefaces with a sample before applying.
        private void ChangeFont()
        {
            var selected = FontWindow.Ask(this, note.fontFamily);
            if (selected != null) note.fontFamily = selected;
        }
        // RGB: shared color editor.
        private void ChangeColor(string current, string defaultColor, Action<string> apply)
        {
            var value = ColorWindow.Ask(this, current, defaultColor);
            if (value == null) return;
            apply(value);
        }
        // MODE: requested mode stays checked after fallback.
        private MenuItem ModeMenu()
        {
            var menu = new MenuItem { Header = Loc.Get("Mode") };
            foreach (var mode in new[] { "normal", "topmost", "desktop" })
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
        // UI: Alt+F4 and taskbar close also require deletion confirmation.
        private void OnClosing(object sender, CancelEventArgs e) { if (!destroying) { e.Cancel = true; Dispatcher.BeginInvoke(new Action(Delete)); } }
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
