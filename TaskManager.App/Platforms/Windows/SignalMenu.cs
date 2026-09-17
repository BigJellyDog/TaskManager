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

    /// <summary>Открывает список текстов под <paramref name="anchor"/>; сообщает номер выбранного пункта.</summary>
    public static void ShowList(
        FrameworkElement anchor, IReadOnlyList<string> texts, int selected, Action<int> chosen, bool mono)
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

        for (var i = 0; i < texts.Count; i++)
        {
            var index = i;
            var isSelected = i == selected;

            var text = new TextBlock
            {
                Text = mono ? texts[i].ToUpperInvariant() : texts[i],
                Foreground = Brush(isSelected ? "Accent" : "Ink"),
                FontFamily = mono ? new FontFamily("Consolas") : new FontFamily("Segoe UI Variable"),
                FontSize = mono ? 12 : 13,
                CharacterSpacing = mono ? 40 : 0,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // Обычный Border, а не Button: шаблон кнопки красит наведение своим цветом.
            var item = new Border
            {
                Child = text,
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
