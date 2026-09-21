using TaskManager.App.Services;

namespace TaskManager.App.Controls;

/// <summary>
/// Поле даты внутри <see cref="InputBox"/>: значок календаря и дата «дд.мм.гггг». Нажатие открывает календарь Signal
/// под полем. Вместо системного DatePicker, который рисует Windows по своей теме.
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

    // Подпись уже построенного поля не перекрашивалась при смене темы на лету: цвета берутся заново.
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        _label.SetDynamicResource(Label.TextColorProperty, "Ink");
        _icon.SetDynamicResource(Icon.ColorProperty, "InkMuted");
    }

    private void Apply() => _label.Text = Date.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);

    private void Open()
    {
        // Нажатие по всему полю: календарь раскрывается под рамкой, в которую вложено это поле.
        var anchor = Parent as VisualElement ?? this;
        if (!anchor.IsEnabled)
        {
            return;
        }

        CalendarPopup.Show(anchor, Date, picked => Date = picked, Loc.T("Сегодня"));
    }
}

/// <summary>Открывает календарь Signal под элементом. Сейчас только Windows.</summary>
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
