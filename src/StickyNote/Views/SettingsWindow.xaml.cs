using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
        // INIT: wire owned state and events.
        public SettingsWindow(App app)
        {
            this.app = app; InitializeComponent(); var settings = app.Notes.Settings;
            Title = Loc.Get("Settings"); Startup.Content = Loc.Get("Startup"); Startup.IsChecked = settings.startWithWindows;
            BackgroundLabel.Text = Loc.Get("Background"); ForegroundLabel.Text = Loc.Get("Foreground"); FontLabel.Text = Loc.Get("FontSize"); OpacityLabel.Text = Loc.Get("Opacity"); ModeLabel.Text = Loc.Get("Mode"); LanguageLabel.Text = Loc.Get("Language"); DefaultsHint.Text = Loc.Get("DefaultsHint");
            ExportButton.Content = Loc.Get("Export"); RestoreButton.Content = Loc.Get("Restore"); SaveButton.Content = Loc.Get("Save");
            foreach (var color in Validation.Colors) BackgroundChoice.Items.Add(color); BackgroundChoice.Text = settings.backgroundColor;
            ForegroundInput.Text = settings.foregroundColor; FontInput.Text = settings.fontSize.ToString(CultureInfo.CurrentCulture); OpacityInput.Value = settings.opacity;
            foreach (var mode in new[] { "desktop", "normal", "topmost" }) ModeChoice.Items.Add(new ComboBoxItem { Content = Loc.Get(mode), Tag = mode });
            ModeChoice.SelectedIndex = Array.IndexOf(new[] { "desktop", "normal", "topmost" }, settings.displayMode);
            LanguageChoice.Items.Add("한국어"); LanguageChoice.Items.Add("English"); LanguageChoice.SelectedIndex = settings.language == "en" ? 1 : 0;
        }
        // CFG: validate and persist defaults.
        private async void SaveClick(object sender, RoutedEventArgs e)
        {
            double font;
            if (!double.TryParse(FontInput.Text, out font)) { MessageBox.Show(this, Loc.Get("Invalid")); return; }
            var settings = new Settings { startWithWindows = Startup.IsChecked == true, backgroundColor = BackgroundChoice.Text, foregroundColor = ForegroundInput.Text, fontSize = font, opacity = OpacityInput.Value, displayMode = (string)((ComboBoxItem)ModeChoice.SelectedItem).Tag, language = LanguageChoice.SelectedIndex == 1 ? "en" : "ko" };
            try
            {
                Validation.Settings(settings); StartupService.Apply(settings.startWithWindows);
                app.Notes.Settings = settings; app.RequestSave(); await app.FlushAsync();
                if (Loc.Culture.TwoLetterISOLanguageName != settings.language) MessageBox.Show(this, Loc.Get("RestartLanguage"));
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
