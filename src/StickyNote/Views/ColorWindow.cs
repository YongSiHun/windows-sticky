using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StickyNote.Utilities;

namespace StickyNote.Views
{
    public sealed class ColorWindow : Window
    {
        private readonly Slider red, green, blue;
        private readonly Border preview;
        private readonly TextBlock value;
        internal string SelectedColor { get; private set; }

        // RGB: shared mouse/keyboard picker with live preview.
        internal ColorWindow(string current)
        {
            Title = Loc.Get("ChooseColor");
            Width = 360; SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
            Background = Brushes.White;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var color = (Color)ColorConverter.ConvertFromString(current);
            var panel = new StackPanel { Margin = new Thickness(18) };
            preview = new Border { Height = 64, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 12) };
            value = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(8) };
            preview.Child = value;
            panel.Children.Add(preview);
            red = AddChannel(panel, "Red", color.R);
            green = AddChannel(panel, "Green", color.G);
            blue = AddChannel(panel, "Blue", color.B);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var cancel = new Button { Content = Loc.Get("Cancel"), IsCancel = true, MinWidth = 72, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4, 0, 4, 0) };
            var save = new Button { Content = Loc.Get("Save"), IsDefault = true, MinWidth = 72, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4, 0, 0, 0) };
            save.Click += (s, e) => DialogResult = true;
            buttons.Children.Add(cancel); buttons.Children.Add(save);
            panel.Children.Add(buttons); Content = panel;
            RefreshPreview();
        }

        // CH: integer RGB slider; accessible channel name.
        private Slider AddChannel(Panel panel, string key, byte initial)
        {
            panel.Children.Add(new TextBlock { Text = Loc.Get(key), Margin = new Thickness(0, 6, 0, 2) });
            var slider = new Slider { Minimum = 0, Maximum = 255, Value = initial, TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 16, Height = 24, Margin = new Thickness(0, 3, 0, 6), AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft, AutoToolTipPrecision = 0 };
            System.Windows.Automation.AutomationProperties.SetName(slider, Loc.Get(key));
            slider.ValueChanged += ChannelChanged;
            panel.Children.Add(slider);
            return slider;
        }

        // UI: update only after all channels exist.
        private void ChannelChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RefreshPreview();
        }

        // HEX: preview and result from the same RGB values.
        private void RefreshPreview()
        {
            var color = Color.FromRgb((byte)red.Value, (byte)green.Value, (byte)blue.Value);
            SelectedColor = string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
            preview.Background = new SolidColorBrush(color);
            value.Text = SelectedColor;
            value.Foreground = new SolidColorBrush(color.R * 299 + color.G * 587 + color.B * 114 > 150000 ? Colors.Black : Colors.White);
        }

        // UI: cancel leaves the caller's color unchanged.
        public static string Ask(Window owner, string current)
        {
            var dialog = new ColorWindow(current) { Owner = owner, Icon = owner.Icon };
            return dialog.ShowDialog() == true ? dialog.SelectedColor : null;
        }
    }
}
