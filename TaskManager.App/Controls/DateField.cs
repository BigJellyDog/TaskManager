using TaskManager.App.Services;

namespace TaskManager.App.Controls;

/// <summary>
/// The date field inside <see cref="InputBox"/>: a calendar icon and the date "dd.mm.yyyy". A tap opens the Signal calendar
/// under the field. Instead of the system DatePicker, which Windows draws in its own theme.
/// </summary>
public sealed class DateField : HorizontalStackLayout
{
    public static readonly BindableProperty DateProperty =
        BindableProperty.Create(nameof(Date), typeof(DateTime), typeof(DateField), DateTime.Today, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((DateField)b).Apply());

    private readonly Label _label = new() { FontFamily = "Consolas", FontSize = 14, VerticalOptions = LayoutOptions.Center };
    private readonly Icon _icon;

    public DateField()
    {
        Spacing = 8;
        VerticalOptions = LayoutOptions.Center;
        BackgroundColor = Colors.Transparent;

        var icon = new Icon { Kind = "calendar", Size = 14 };
        icon.SetDynamicResource(Icon.ColorProperty, "InkMuted");
        _label.SetDynamicResource(Label.TextColorProperty, "Ink");

        Children.Add(icon);
        Children.Add(_label);

        _icon = icon;
        ThemeManager.Changed += OnThemeChanged;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Open();
        GestureRecognizers.Add(tap);
        Apply();
    }

    public DateTime Date
    {
        get => (DateTime)GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    // The label of an already built field was not recoloured on a live theme change: the colours are taken again.
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _label.SetDynamicResource(Label.TextColorProperty, "Ink");
        _icon.SetDynamicResource(Icon.ColorProperty, "InkMuted");
    }

    private void Apply() => _label.Text = Date.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private void Open()
    {
        // A tap anywhere on the field: the calendar opens under the border that contains this field.
        var anchor = Parent as VisualElement ?? this;
        if (!anchor.IsEnabled)
        {
            return;
        }

        CalendarPopup.Show(anchor, Date, picked => Date = picked, Loc.T("Сегодня"));
    }
}

/// <summary>Opens the Signal calendar under an element. Windows only for now.</summary>
public static class CalendarPopup
{
    public static void Show(VisualElement anchor, DateTime selected, Action<DateTime> chosen, string todayText)
    {
#if WINDOWS
        if (anchor.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement native)
        {
            Platforms.Windows.SignalCalendar.Show(native, selected, Loc.Culture, todayText, chosen);
        }
#endif
    }
}
