using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using StickyNote.Utilities;
using Validation = StickyNote.Utilities.Validation;

namespace StickyNote.Views
{
    public sealed class ColorWindow : Window
    {
        private readonly Slider red, green, blue;
        private readonly Border preview;
        private readonly TextBlock value;
        private readonly Grid colorField;
        private readonly Slider hue;
        private readonly Border pureColor;
        private readonly Canvas cursorLayer;
        private readonly System.Windows.Shapes.Ellipse cursor;
        private bool selecting;
        internal string SelectedColor { get; private set; }

        // RGB: shared mouse/keyboard picker with live preview.
        internal ColorWindow(string current, string defaultColor)
        {
            Title = Loc.Get("ChooseColor");
            Width = 400; SizeToContent = SizeToContent.Height; MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 60);
            ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
            Background = Brushes.White;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var color = (Color)ColorConverter.ConvertFromString(current);
            var panel = new StackPanel { Margin = new Thickness(18) };
            preview = new Border { Height = 64, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 12) };
            value = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(8) };
            preview.Child = value;
            panel.Children.Add(preview);
            panel.Children.Add(new TextBlock { Text = Loc.Get("ColorDragHint"), Margin = new Thickness(0, 0, 0, 6) });
            colorField = new Grid { Height = 150, ClipToBounds = true, Cursor = Cursors.Cross, Margin = new Thickness(0, 0, 0, 8) };
            pureColor = new Border(); colorField.Children.Add(pureColor);
            var white = new LinearGradientBrush(Colors.White, Colors.Transparent, 0);
            colorField.Children.Add(new Border { Background = white });
            colorField.Children.Add(new Border { Background = new LinearGradientBrush(Colors.Transparent, Colors.Black, 90) });
            cursorLayer = new Canvas { IsHitTestVisible = false };
            cursor = new System.Windows.Shapes.Ellipse { Width = 12, Height = 12, Stroke = Brushes.White, StrokeThickness = 2, Fill = Brushes.Transparent };
            cursorLayer.Children.Add(cursor); colorField.Children.Add(cursorLayer);
            colorField.MouseLeftButtonDown += StartSelection;
            colorField.MouseMove += MoveSelection;
            colorField.MouseLeftButtonUp += (s, e) => colorField.ReleaseMouseCapture();
            colorField.SizeChanged += (s, e) => RefreshPreview();
            panel.Children.Add(colorField);
            panel.Children.Add(new TextBlock { Text = Loc.Get("Hue") });
            hue = new Slider { Minimum = 0, Maximum = 359.99, Margin = new Thickness(0, 4, 0, 10), Height = 24 };
            var rainbow = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
            for (int i = 0; i <= 6; i++) rainbow.GradientStops.Add(new GradientStop(FromHsv(i * 60, 1, 1), i / 6d));
            hue.Background = rainbow;
            System.Windows.Automation.AutomationProperties.SetName(hue, Loc.Get("Hue"));
            hue.ValueChanged += HueChanged;
            panel.Children.Add(hue);
            panel.Children.Add(new TextBlock { Text = Loc.Get("Palette"), Margin = new Thickness(0, 0, 0, 6) });
            panel.Children.Add(CreatePalette());
            red = AddChannel(panel, "Red", color.R);
            green = AddChannel(panel, "Green", color.G);
            blue = AddChannel(panel, "Blue", color.B);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var reset = new Button { Name = "ResetColorButton", Content = Loc.Get("ResetDefaults"), MinWidth = 72, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 4, 0) };
            reset.Click += (s, e) => SelectColor((Color)ColorConverter.ConvertFromString(defaultColor));
            var cancel = new Button { Content = Loc.Get("Cancel"), IsCancel = true, MinWidth = 72, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4, 0, 4, 0) };
            var save = new Button { Content = Loc.Get("Save"), IsDefault = true, MinWidth = 72, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(4, 0, 0, 0) };
            save.Click += (s, e) => DialogResult = true;
            buttons.Children.Add(reset); buttons.Children.Add(cancel); buttons.Children.Add(save);
            panel.Children.Add(buttons); Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            RefreshPreview();
        }

        // PICK: capture dragging so selecting continues at the palette edges.
        private void StartSelection(object sender, MouseButtonEventArgs e)
        {
            colorField.CaptureMouse(); Pick(e.GetPosition(colorField)); e.Handled = true;
        }
        private void MoveSelection(object sender, MouseEventArgs e)
        {
            if (colorField.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) Pick(e.GetPosition(colorField));
        }
        private void Pick(Point position)
        {
            SelectAt(position.X / colorField.ActualWidth, position.Y / colorField.ActualHeight);
        }
        // PICK: X is saturation, Y is brightness; RGB and preview follow the mouse.
        internal void SelectAt(double x, double y)
        {
            SelectColor(FromHsv(hue.Value, Math.Max(0, Math.Min(1, x)), 1 - Math.Max(0, Math.Min(1, y))));
        }
        private void HueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (selecting || red == null || green == null || blue == null) return;
            var hsv = ToHsv(Color.FromRgb((byte)red.Value, (byte)green.Value, (byte)blue.Value));
            SelectColor(FromHsv(hue.Value, hsv[1], hsv[2]));
        }
        internal static Color FromHsv(double h, double s, double v)
        {
            h = (h % 360 + 360) % 360;
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
            var sectors = new[] { new[] { c, x, 0d }, new[] { x, c, 0d }, new[] { 0d, c, x }, new[] { 0d, x, c }, new[] { x, 0d, c }, new[] { c, 0d, x } };
            var rgb = sectors[(int)(h / 60)];
            return Color.FromRgb((byte)Math.Round((rgb[0] + m) * 255), (byte)Math.Round((rgb[1] + m) * 255), (byte)Math.Round((rgb[2] + m) * 255));
        }
        private static double[] ToHsv(Color color)
        {
            double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
            double h = 0;
            if (delta > 0) h = Hue(r, g, b, max, delta);
            return new[] { h, max == 0 ? 0 : delta / max, max };
        }
        private static double Hue(double r, double g, double b, double max, double delta)
        {
            if (max == r) return ((g - b) / delta * 60 + 360) % 360;
            if (max == g) return ((b - r) / delta + 2) * 60;
            return ((r - g) / delta + 4) * 60;
        }

        // UI: the same visual palette is available for background and text colors.
        private System.Windows.Controls.Primitives.UniformGrid CreatePalette()
        {
            var palette = new System.Windows.Controls.Primitives.UniformGrid { Columns = 8, Margin = new Thickness(0, 0, 0, 8) };
            foreach (var hex in Validation.Palette)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var swatch = new Button { Width = 30, Height = 30, MinHeight = 0, Padding = new Thickness(0), Margin = new Thickness(3), Background = new SolidColorBrush(color), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), ToolTip = hex, Tag = hex };
                System.Windows.Automation.AutomationProperties.SetName(swatch, Loc.Get("ChooseColor") + " " + hex);
                swatch.Click += (s, e) => SelectColor(color);
                palette.Children.Add(swatch);
            }
            return palette;
        }

        // RGB: selecting a swatch keeps fine adjustment and preview in sync.
        private void SelectColor(Color color)
        {
            selecting = true;
            red.Value = color.R; green.Value = color.G; blue.Value = color.B;
            selecting = false;
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
            if (selecting || red == null || green == null || blue == null) return;
            var color = Color.FromRgb((byte)red.Value, (byte)green.Value, (byte)blue.Value);
            var hsv = ToHsv(color);
            selecting = true;
            if (hsv[1] > 0) hue.Value = hsv[0];
            selecting = false;
            pureColor.Background = new SolidColorBrush(FromHsv(hue.Value, 1, 1));
            Canvas.SetLeft(cursor, hsv[1] * colorField.ActualWidth - 6);
            Canvas.SetTop(cursor, (1 - hsv[2]) * colorField.ActualHeight - 6);
            SelectedColor = string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
            preview.Background = new SolidColorBrush(color);
            value.Text = string.Format("{0}   RGB({1}, {2}, {3})", SelectedColor, color.R, color.G, color.B);
            value.Foreground = new SolidColorBrush(color.R * 299 + color.G * 587 + color.B * 114 > 150000 ? Colors.Black : Colors.White);
        }

        // UI: cancel leaves the caller's color unchanged.
        public static string Ask(Window owner, string current, string defaultColor)
        {
            var dialog = new ColorWindow(current, defaultColor) { Owner = owner, Icon = owner.Icon };
            return dialog.ShowDialog() == true ? dialog.SelectedColor : null;
        }
    }
}
