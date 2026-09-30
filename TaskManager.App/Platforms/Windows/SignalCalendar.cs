namespace TaskManager.App.Platforms.Windows;

using System.Globalization;
using Microsoft.Maui.Platform;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

/// <summary>
/// A calendar under the date field in the Signal style: a month with arrows, weekdays from Monday, a 6×7 grid of days, today
/// in an accent frame, the selected day filled with the accent. It is an ordinary Flyout with its own elements, so it is in the theme of the application
/// (the system calendar is drawn by Windows in its own theme).
/// </summary>
public static class SignalCalendar
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

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];

    /// <summary>Opens the calendar under <paramref name="anchor"/>; <paramref name="chosen"/> receives the selected date.</summary>
    public static void Show(FrameworkElement anchor, DateTime selected, CultureInfo culture, string todayText, Action<DateTime> chosen)
    {
        var style = new Style(typeof(FlyoutPresenter));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("Surface")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("Line")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(14)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10)));

        var flyout = new Flyout { Placement = FlyoutPlacementMode.Bottom, FlyoutPresenterStyle = style };

        var root = new StackPanel
        {
            Spacing = 6,
            Width = 238,
            RequestedTheme = global::TaskManager.App.Services.ThemeManager.NativeTheme,
        };

        flyout.Content = root;
        Build(root, flyout, new DateTime(selected.Year, selected.Month, 1), selected.Date, culture, todayText, chosen);
        flyout.ShowAt(anchor);
    }

    private static void Build(
        StackPanel root, Flyout flyout, DateTime month, DateTime selected, CultureInfo culture, string todayText,
        Action<DateTime> chosen)
    {
        // Changing the month rebuilds the content: the calendar stays open.
        root.Children.Clear();

        var none = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var raised = Brush("SurfaceRaised");
        var mono = new FontFamily("Segoe UI Variable");

        UIElement Arrow(string glyph, int delta)
        {
            var text = new TextBlock
            {
                Text = glyph,
                Foreground = Brush("InkMuted"),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var border = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(4), Background = none, Child = text };
            border.PointerEntered += (_, _) => border.Background = raised;
            border.PointerExited += (_, _) => border.Background = none;
            border.Tapped += (_, _) => Build(root, flyout, month.AddMonths(delta), selected, culture, todayText, chosen);
            return border;
        }

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var previous = Arrow("‹", -1);
        var next = Arrow("›", 1);
        var title = new TextBlock
        {
            Text = Capitalize(culture.DateTimeFormat.GetMonthName(month.Month)) + " " + month.Year.ToString(culture),
            Foreground = Brush("Ink"),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn((FrameworkElement)previous, 0);
        Grid.SetColumn(title, 1);
        Grid.SetColumn((FrameworkElement)next, 2);
        header.Children.Add(previous);
        header.Children.Add(title);
        header.Children.Add(next);
        root.Children.Add(header);

        // Weekdays from Monday.
        var week = new Grid();
        for (var i = 0; i < 7; i++)
        {
            week.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var day = (DayOfWeek)(((i + 1) % 7));
            var label = new TextBlock
            {
                Text = Capitalize(culture.DateTimeFormat.GetShortestDayName(day)).ToUpperInvariant(),
                Foreground = Brush("InkMuted"),
                FontFamily = mono,
                FontSize = 10,
                CharacterSpacing = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Grid.SetColumn(label, i);
            week.Children.Add(label);
        }

        root.Children.Add(week);

        // The grid of days: six weeks, the first starts on the Monday before the first day of the month.
        var grid = new Grid { RowSpacing = 2 };
        for (var i = 0; i < 7; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var i = 0; i < 6; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var offset = ((int)month.DayOfWeek + 6) % 7;
        var start = month.AddDays(-offset);
        var today = DateTime.Today;

        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            var inMonth = date.Month == month.Month;
            var isSelected = date == selected;
            var isToday = date == today;

            var text = new TextBlock
            {
                Text = date.Day.ToString(culture),
                FontFamily = mono,
                FontSize = 12,
                FontWeight = isSelected || isToday ? FontWeights.Bold : FontWeights.Normal,
                Foreground = Brush(isSelected ? "OnAccent" : isToday ? "Accent" : inMonth ? "Ink" : "InkMuted"),
                Opacity = inMonth || isSelected ? 1 : 0.55,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var cell = new Border
            {
                Height = 30,
                CornerRadius = new CornerRadius(4),
                Background = isSelected ? Brush("Accent") : none,
                BorderBrush = isToday && !isSelected ? Brush("Accent") : none,
                BorderThickness = new Thickness(1),
                Child = text,
            };

            if (!isSelected)
            {
                cell.PointerEntered += (_, _) => cell.Background = raised;
                cell.PointerExited += (_, _) => cell.Background = none;
            }

            var picked = date;
            cell.Tapped += (_, _) =>
            {
                flyout.Hide();
                chosen(picked);
            };

            Grid.SetRow(cell, i / 7);
            Grid.SetColumn(cell, i % 7);
            grid.Children.Add(cell);
        }

        root.Children.Add(grid);

        // Bottom row: a quick jump to today.
        var todayLink = new TextBlock
        {
            Text = todayText,
            Foreground = Brush("Accent"),
            FontSize = 12,
            Margin = new Thickness(2, 4, 0, 0),
        };
        var todayButton = new Border { Child = todayLink, Background = none, HorizontalAlignment = HorizontalAlignment.Left };
        todayButton.Tapped += (_, _) =>
        {
            flyout.Hide();
            chosen(DateTime.Today);
        };
        root.Children.Add(todayButton);
    }
}
