namespace TaskManager.App.Platforms.Windows;

using Microsoft.Maui.Platform;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

/// <summary>
/// Список под раскрывающимся чипом или полем в стиле Signal: поверхность и рамка из токенов, моноширинный текст,
/// подсветка строки под указателем, выбранный пункт акцентом. Это обычный Flyout со своими элементами, поэтому он
/// всегда в теме приложения (системный список Picker рисует Windows по своей теме).
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
        // Сетка 16x16 как у значка приоритета в списке: x, высота столбиков 4, 7, 10, 13 от нижнего края.
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

    /// <summary>Открывает список текстов под <paramref name="anchor"/>; сообщает номер выбранного пункта.</summary>
    public static void ShowList(
        FrameworkElement anchor, IReadOnlyList<global::TaskManager.App.Controls.DropEntry> entries, int selected, Action<int> chosen, bool mono)
    {
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("Surface")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("Line")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(6)));
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
                // Теги в макете строчными (#работа), приоритеты и служебные подписи заглавными.
                Text = mono && !entry.Muted ? entry.Text.ToUpperInvariant() : entry.Text,
                Foreground = Brush(isSelected && entry.Level == 0 ? "Accent" : colorKey),
                FontFamily = mono ? new FontFamily("Consolas") : new FontFamily("Segoe UI Variable"),
                FontSize = mono ? 12 : 13,
                CharacterSpacing = mono && !entry.Muted ? 40 : 0,
                FontWeight = isSelected || entry.Level > 0 ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Приоритет: четыре столбика и подпись в цвете приоритета, как в списке и на карточках.
            // Как в макете: слева место под галочку выбранного пункта, справа приглушённая подпись.
            var check = new TextBlock
            {
                Text = isSelected ? "✓" : string.Empty,
                Width = 14,
                Foreground = Brush("Accent"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };

            UIElement content = text;
            var line = new Grid { ColumnSpacing = 8 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(check, 0);
            line.Children.Add(check);

            if (entry.Level > 0)
            {
                var bars = PriorityBars(entry.Level, colorKey);
                Grid.SetColumn(bars, 1);
                line.Children.Add(bars);
            }

            Grid.SetColumn((FrameworkElement)text, 2);
            line.Children.Add(text);

            if (!string.IsNullOrEmpty(entry.Hint))
            {
                var hint = new TextBlock
                {
                    Text = entry.Hint,
                    Foreground = Brush("InkMuted"),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Margin = new Thickness(14, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(hint, 3);
                line.Children.Add(hint);
            }

            content = line;

            // Обычный Border, а не Button: шаблон кнопки красит наведение своим цветом.
            var item = new Border
            {
                Child = (UIElement)content,
                Background = none,
                CornerRadius = new CornerRadius(4),
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
