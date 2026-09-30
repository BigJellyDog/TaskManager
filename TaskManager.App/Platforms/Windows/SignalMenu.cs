namespace TaskManager.App.Platforms.Windows;

using Microsoft.Maui.Platform;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

/// <summary>
/// A list under a drop-down chip or field in the Signal style: the surface and the border from tokens, monospaced text,
/// highlight of the row under the pointer, the selected item in the accent. It is an ordinary Flyout with its own elements, so it is
/// always in the theme of the application (the system Picker list is drawn by Windows in its own theme).
/// </summary>
public static class SignalMenu
{
    private static global::Windows.UI.Color Token(string key)
    {
        var color = Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
                    && value is Microsoft.Maui.Graphics.Color c
            ? c
            : Microsoft.Maui.Graphics.Colors.Gray;

        return color.ToWindowsColor();
    }

    private static SolidColorBrush Brush(string key) => new(Token(key));

    private static Canvas PriorityBars(int level, string colorKey)
    {
        // A 16x16 grid like the priority icon in the list: x, the bar heights 4, 7, 10, 13 from the bottom edge.
        var canvas = new Canvas { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        var heights = new[] { 4, 7, 10, 13 };
        for (var i = 0; i < 4; i++)
        {
            var color = Token(colorKey);
            if (i >= level)
            {
                color.A = (byte)(color.A * 0.28);
            }

            var bar = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = 3,
                Height = heights[i],
                RadiusX = 1,
                RadiusY = 1,
                Fill = new SolidColorBrush(color),
            };
            Canvas.SetLeft(bar, 1 + (i * 4));
            Canvas.SetTop(bar, 15 - heights[i]);
            canvas.Children.Add(bar);
        }

        return canvas;
    }

    /// <summary>Opens a list of texts under <paramref name="anchor"/>; reports the number of the chosen item.</summary>
    public static void ShowList(
        FrameworkElement anchor, IReadOnlyList<global::TaskManager.App.Controls.DropEntry> entries, int selected, Action<int> chosen, bool mono)
    {
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("Surface")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("Line")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(14)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));
        style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, Math.Max(anchor.ActualWidth, 140)));
        style.Setters.Add(new Setter(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden));

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom, FlyoutPresenterStyle = style };

        var panel = new StackPanel
        {
            Spacing = 1,
            RequestedTheme = global::TaskManager.App.Services.ThemeManager.NativeTheme,
        };

        var raised = Brush("SurfaceRaised");
        var none = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        for (var i = 0; i < entries.Count; i++)
        {
            var index = i;
            var isSelected = i == selected;

            var entry = entries[i];
            var colorKey = entry.ColorKey ?? (isSelected ? "Accent" : entry.Muted ? "InkMuted" : "Ink");

            var text = new TextBlock
            {
                // Tags in lower case in the mockup (#work), priorities and service labels in upper case.
                Text = mono && !entry.Muted ? entry.Text.ToUpperInvariant() : entry.Text,
                Foreground = Brush(isSelected && entry.Level == 0 ? "Accent" : colorKey),
                FontFamily = mono ? new FontFamily("Segoe UI Variable") : new FontFamily("Segoe UI Variable"),
                FontSize = mono ? 12 : 13,
                CharacterSpacing = mono && !entry.Muted ? 40 : 0,
                FontWeight = isSelected || entry.Level > 0 ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // The selected item is marked with colour and a bold font, without a check mark. Priority: four bars and a label in the colour of
            // the priority, as in the list and on cards; a muted label on the right. The padding comes only from what is in the row.
            UIElement content = text;
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (entry.Level > 0)
            {
                var bars = PriorityBars(entry.Level, colorKey);
                bars.Margin = new Thickness(0, 0, 8, 0);
                Grid.SetColumn(bars, 0);
                line.Children.Add(bars);
            }

            if (entry.Swatch is { Length: 3 })
            {
                var swatch = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                foreach (var hex in entry.Swatch)
                {
                    var color = Microsoft.Maui.Graphics.Color.FromArgb(hex).ToWindowsColor();
                    swatch.Children.Add(new Border
                    {
                        Width = 10,
                        Height = 14,
                        CornerRadius = new CornerRadius(2),
                        Background = new SolidColorBrush(color),
                        BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(115, 128, 128, 128)),
                        BorderThickness = new Thickness(1),
                    });
                }

                swatch.Margin = new Thickness(0, 0, 8, 0);
                Grid.SetColumn(swatch, 1);
                line.Children.Add(swatch);
            }

            Grid.SetColumn((FrameworkElement)text, 2);
            line.Children.Add(text);

            if (!string.IsNullOrEmpty(entry.Hint))
            {
                var hint = new TextBlock
                {
                    Text = entry.Hint,
                    Foreground = Brush("InkMuted"),
                    FontFamily = new FontFamily("Segoe UI Variable"),
                    FontSize = 11,
                    Margin = new Thickness(14, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(hint, 3);
                line.Children.Add(hint);
            }

            content = line;

            // An ordinary Border, not a Button: the button template paints the hover state in its own colour.
            var item = new Border
            {
                Child = (UIElement)content,
                Background = none,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 0, 10, 0),
                MinHeight = 30,
            };

            item.PointerEntered += (_, _) => item.Background = raised;
            item.PointerExited += (_, _) => item.Background = none;
            item.Tapped += (_, _) =>
            {
                flyout.Hide();
                chosen(index);
            };

            panel.Children.Add(item);
        }

        flyout.Content = new ScrollViewer
        {
            Content = panel,
            MaxHeight = 320,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };

        flyout.ShowAt(anchor);
    }
}
