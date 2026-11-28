using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StickyNote.Utilities;

namespace StickyNote.Views
{
    public sealed class FontWindow : Window
    {
        internal ComboBox FontChoice { get; private set; }
        internal TextBlock Preview { get; private set; }
        internal string SelectedFont
        {
            get
            {
                return (string)FontChoice.SelectedItem;
            }
        }

        // Offer installed fonts while retaining a saved font that is unavailable locally.
        internal FontWindow(string current)
        {
            Title = Loc.Get("FontFamily");
            Width = 380;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var panel = new StackPanel
            {
                Margin = new Thickness(18)
            };
            panel.Children.Add(new TextBlock
            {
                Text = Loc.Get("FontFamily"),
                Margin = new Thickness(0, 0, 0, 8)
            });
            var fonts = Fonts.SystemFontFamilies.Select(font => font.Source).Concat(new[] { current }).Distinct().OrderBy(name => name).ToArray();
            FontChoice = new ComboBox
            {
                ItemsSource = fonts,
                SelectedItem = current,
                MaxDropDownHeight = 240
            };
            System.Windows.Automation.AutomationProperties.SetName(FontChoice, Loc.Get("FontFamily"));
            Preview = new TextBlock
            {
                Text = Loc.Get("FontSample"),
                FontFamily = new FontFamily(current),
                FontSize = 22,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 18, 0, 18)
            };
            FontChoice.SelectionChanged += (s, e) => Preview.FontFamily = new FontFamily(SelectedFont);
            panel.Children.Add(FontChoice);
            panel.Children.Add(Preview);
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancel = new Button
            {
                Content = Loc.Get("Cancel"),
                IsCancel = true,
                MinWidth = 72
            };
            var save = new Button
            {
                Content = Loc.Get("Save"),
                IsDefault = true,
                MinWidth = 72
            };
            save.Click += (s, e) => DialogResult = true;
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            panel.Children.Add(buttons);
            Content = panel;
        }

        // Return the chosen font only when the user accepts the dialog.
        public static string Ask(Window owner, string current)
        {
            var dialog = new FontWindow(current)
            {
                Owner = owner,
                Icon = owner.Icon
            };
            return dialog.ShowDialog() == true ? dialog.SelectedFont : null;
        }
    }
}
