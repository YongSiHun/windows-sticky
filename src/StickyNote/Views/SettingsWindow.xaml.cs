using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using StickyNote.Models;
using StickyNote.Services;
using StickyNote.Utilities;
using Validation = StickyNote.Utilities.Validation;

namespace StickyNote.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly App app;
        private string backgroundColor, foregroundColor, fontFamily;
        // INIT: wire owned state and events.
        public SettingsWindow(App app)
        {
            this.app = app; InitializeComponent(); var settings = app.Notes.Settings;
            Title = Loc.Get("Settings"); Startup.Content = Loc.Get("Startup"); Startup.IsChecked = settings.startWithWindows;
            BackgroundLabel.Text = Loc.Get("Background"); ForegroundLabel.Text = Loc.Get("Foreground"); FontLabel.Text = Loc.Get("FontSize"); OpacityLabel.Text = Loc.Get("Opacity"); ModeLabel.Text = Loc.Get("Mode"); DefaultsHint.Text = Loc.Get("DefaultsHint");
            BackgroundButton.Content = ForegroundButton.Content = Loc.Get("ChooseColor");
            ExportButton.Content = Loc.Get("Export"); RestoreButton.Content = Loc.Get("Restore"); SaveButton.Content = Loc.Get("Save");
            backgroundColor = settings.backgroundColor; foregroundColor = settings.foregroundColor;
            fontFamily = settings.fontFamily; FontFamilyLabel.Text = Loc.Get("FontFamily"); FontFamilyButton.Content = fontFamily;
            RefreshColors(); FontInput.Value = settings.fontSize; OpacityInput.Value = settings.opacity;
            VersionLabel.Text = Loc.Get("Version");
            VersionValue.Text = typeof(App).Assembly.GetName().Version.ToString(3);
            UpdateButton.Content = Loc.Get("CheckUpdates"); DownloadsButton.Content = Loc.Get("OpenDownloads");
            foreach (var mode in new[] { "desktop", "normal", "topmost" }) ModeChoice.Items.Add(new ComboBoxItem { Content = Loc.Get(mode), Tag = mode });
            ModeChoice.SelectedIndex = Array.IndexOf(new[] { "desktop", "normal", "topmost" }, settings.displayMode);
        }
        // VER: check on demand; never block editing with network I/O.
        private async void CheckUpdatesClick(object sender, RoutedEventArgs e)
        {
            UpdateButton.IsEnabled = false;
            DownloadsButton.Visibility = Visibility.Collapsed;
            UpdateStatus.Text = Loc.Get("CheckingUpdates");
            try
            {
                var latest = await UpdateService.CheckAsync();
                ShowUpdateResult(latest);
            }
            catch (Exception error) { app.LogUpdateFailure(error); UpdateStatus.Text = Loc.Get("UpdateFailed"); }
            finally { UpdateButton.IsEnabled = true; }
        }
        // VER: separate result presentation from transport.
        private void ShowUpdateResult(Version latest)
        {
            if (latest <= UpdateService.Current) { UpdateStatus.Text = Loc.Get("LatestVersion"); return; }
            UpdateStatus.Text = string.Format(Loc.Get("UpdateAvailable"), latest.ToString(3));
            DownloadsButton.Visibility = Visibility.Visible;
        }
        // WEB: fixed project downloads page, opened only by user click.
        private void DownloadsClick(object sender, RoutedEventArgs e)
        {
            try { System.Diagnostics.Process.Start(UpdateService.DownloadsUrl); }
            catch (Exception error) { app.Report("update-downloads", error); }
        }
        // BG: mouse picker edits the new-note default.
        private void BackgroundClick(object sender, RoutedEventArgs e)
        {
            var color = ColorWindow.Ask(this, backgroundColor);
            if (color == null) return;
            backgroundColor = color; RefreshColors();
        }
        // FG: mouse picker edits the new-note default.
        private void ForegroundClick(object sender, RoutedEventArgs e)
        {
            var color = ColorWindow.Ask(this, foregroundColor);
            if (color == null) return;
            foregroundColor = color; RefreshColors();
        }
        private void FontFamilyClick(object sender, RoutedEventArgs e)
        {
            var selected = FontWindow.Ask(this, fontFamily);
            if (selected == null) return;
            fontFamily = selected; FontFamilyButton.Content = selected;
        }
        // RGB: selected swatches replace code input fields.
        private void RefreshColors()
        {
            SetSwatch(BackgroundButton, backgroundColor);
            SetSwatch(ForegroundButton, foregroundColor);
        }
        // UI: keep picker text readable on every selected color.
        private static void SetSwatch(Button button, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            button.Background = new SolidColorBrush(color);
            button.Foreground = new SolidColorBrush(color.R * 299 + color.G * 587 + color.B * 114 > 150000 ? Colors.Black : Colors.White);
        }
        // CFG: validate and persist defaults.
        private async void SaveClick(object sender, RoutedEventArgs e)
        {
            var settings = new Settings { startWithWindows = Startup.IsChecked == true, backgroundColor = backgroundColor, foregroundColor = foregroundColor, fontSize = FontInput.Value, fontFamily = fontFamily, opacity = OpacityInput.Value, displayMode = (string)((ComboBoxItem)ModeChoice.SelectedItem).Tag, language = app.Notes.Settings.language };
            try
            {
                Validation.Settings(settings); StartupService.Apply(settings.startWithWindows);
                app.Notes.Settings = settings; app.RequestSave(); await app.FlushAsync();
                Close();
            }
            catch (Exception error) { app.Report("settings-save", error); }
        }
        // BAK: capture on dispatcher, export off thread.
        private async void ExportClick(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "sticky-note-backup.json", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            try { var data = app.Notes.Snapshot(); await Task.Run(() => app.Backups.Export(dialog.FileName, data)); MessageBox.Show(this, Loc.Get("Success")); }
            catch (Exception error) { app.Report("backup-export", error); }
        }
        // REC: validate backup and confirm restore.
        private async void RestoreClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" }; if (dialog.ShowDialog(this) != true) return;
            try
            {
                var data = await Task.Run(() => app.Backups.Import(dialog.FileName));
                if (MessageBox.Show(this, Loc.Get("RestoreConfirm"), Loc.Get("AppTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                IsEnabled = false; await app.RestoreAsync(data); Close();
            }
            catch (Exception error) { IsEnabled = true; app.Report("backup-restore", error); }
        }
    }
}
