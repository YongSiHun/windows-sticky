using System.Windows;
using StickyNote.Services;
using StickyNote.Utilities;
using StickyNote.ViewModels;

namespace StickyNote.Views
{
    public partial class NoteManagerWindow : Window
    {
        private readonly NoteService service;
        private readonly NoteManagerViewModel model;
        public NoteManagerWindow(NoteService service)
        {
            this.service = service; InitializeComponent(); model = new NoteManagerViewModel(service); DataContext = model;
            Title = Heading.Text = Loc.Get("Manager"); SearchBox.ToolTip = Loc.Get("Search");
            System.Windows.Automation.AutomationProperties.SetName(SearchBox, Loc.Get("Search"));
            NewButton.Content = Loc.Get("NewNote"); ShowButton.Content = Loc.Get("Show"); HideButton.Content = Loc.Get("Hide"); DeleteButton.Content = Loc.Get("Delete");
            ShowAllButton.Content = Loc.Get("ShowAll"); HideAllButton.Content = Loc.Get("HideAll"); SettingsButton.Content = Loc.Get("Settings");
        }
        private void NewClick(object s, RoutedEventArgs e) { service.Create(); }
        private void ShowClick(object s, RoutedEventArgs e) { if (model.Selected != null) service.Show(model.Selected); }
        private void HideClick(object s, RoutedEventArgs e) { if (model.Selected != null) service.Hide(model.Selected); }
        private void DeleteClick(object s, RoutedEventArgs e) { if (model.Selected != null && MessageBox.Show(this, Loc.Get("DeleteConfirm"), Loc.Get("AppTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes) service.Delete(model.Selected); }
        private void ShowAllClick(object s, RoutedEventArgs e) { service.ShowAll(); }
        private void HideAllClick(object s, RoutedEventArgs e) { service.HideAll(); }
        private void SettingsClick(object s, RoutedEventArgs e) { ((App)Application.Current).ShowSettings(); }
    }
}
