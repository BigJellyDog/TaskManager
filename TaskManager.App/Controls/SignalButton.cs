using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

public enum ButtonVariant
{
    Primary,
    Secondary,
    Ghost,
    Danger,
}

/// <summary>
/// Кнопка Signal: иконка, подпись и необязательная подсказка горячей клавиши. Основная (лайм) со свечением,
/// обычная с рамкой, прозрачная и опасная (красный текст). Недоступная кнопка остаётся на месте, но серая.
/// </summary>
public sealed class SignalButton : Border
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(SignalButton), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(SignalButton), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty HintProperty =
        BindableProperty.Create(nameof(Hint), typeof(string), typeof(SignalButton), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty VariantProperty =
        BindableProperty.Create(nameof(Variant), typeof(ButtonVariant), typeof(SignalButton), ButtonVariant.Secondary, propertyChanged: OnChanged);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SignalButton), null, propertyChanged: OnCommandChanged);

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SignalButton));

    public static readonly BindableProperty IconAfterProperty =
        BindableProperty.Create(nameof(IconAfter), typeof(bool), typeof(SignalButton), false, propertyChanged: OnChanged);

    public static readonly BindableProperty CompactProperty =
        BindableProperty.Create(nameof(Compact), typeof(bool), typeof(SignalButton), false, propertyChanged: OnChanged);

    private readonly Icon _icon = new() { Size = 16 };
    private readonly Label _label = new() { FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
    private readonly Label _hint = new() { FontFamily = "Segoe UI Variable", FontSize = 11, VerticalOptions = LayoutOptions.Center };
    private readonly Border _hintBox;
    private bool _hover;
    private readonly HorizontalStackLayout _row = new() { Spacing = 7, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

    public SignalButton()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 18 };
        StrokeThickness = 1;
        Padding = new Thickness(16, 0);
        HeightRequest = 36;
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Center;

        _hintBox = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            StrokeThickness = 1,
            Padding = new Thickness(5, 0),
            VerticalOptions = LayoutOptions.Center,
            Content = _hint,
        };

        _row.Children.Add(_icon);
        _row.Children.Add(_label);
        _row.Children.Add(_hintBox);
        Content = _row;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (IsEnabled && Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        };
        GestureRecognizers.Add(tap);

        // Под указателем: обычная и опасная кнопки светлеют до «приподнятой» поверхности, прозрачная получает такой же
        // фон и яркий текст, основная становится чуть ярче.
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => SetHover(true);
        pointer.PointerExited += (_, _) => SetHover(false);
        GestureRecognizers.Add(pointer);

        Apply();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Hint
    {
        get => (string)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    public ButtonVariant Variant
    {
        get => (ButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>Иконка стоит справа от подписи.</summary>
    public bool IconAfter
    {
        get => (bool)GetValue(IconAfterProperty);
        set => SetValue(IconAfterProperty, value);
    }

    /// <summary>Малая кнопка (высота 28, подпись обычной жирности).</summary>
    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled))
        {
            Apply();
        }
    }

    private static Color? TryGetColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : null;

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((SignalButton)bindable).Apply();

    private static void OnCommandChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var button = (SignalButton)bindable;
        if (oldValue is ICommand old)
        {
            old.CanExecuteChanged -= button.OnCanExecuteChanged;
        }

        if (newValue is ICommand command)
        {
            command.CanExecuteChanged += button.OnCanExecuteChanged;
        }

        button.Apply();
    }

    private void SetHover(bool hover)
    {
        _hover = hover;
        Apply();
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(Apply);

    private void Apply()
    {
        var enabled = IsEnabled && (Command?.CanExecute(CommandParameter) ?? true);

        _row.Children.Clear();
        if (IconAfter)
        {
            _row.Children.Add(_label);
            _row.Children.Add(_icon);
        }
        else
        {
            _row.Children.Add(_icon);
            _row.Children.Add(_label);
        }

        _row.Children.Add(_hintBox);

        // Привязка к ещё не заполненной модели может вернуть null: тогда считаем текст пустым.
        var text = Text ?? string.Empty;
        var icon = Icon ?? string.Empty;
        var hint = Hint ?? string.Empty;

        _label.Text = text;
        _label.IsVisible = text.Length > 0;
        _icon.Kind = icon;
        _icon.IsVisible = icon.Length > 0;
        _hint.Text = hint;
        _hintBox.IsVisible = hint.Length > 0;
        // Высоту, заданную в XAML (например, 42 у главной кнопки карточки), не затираем: стандартные 28/32 подставляем сами.
        if (HeightRequest is -1 or 28 or 32 or 36)
        {
            HeightRequest = Compact ? 28 : 36;
        }

        Padding = Compact ? new Thickness(12, 0) : new Thickness(16, 0);
        _label.FontAttributes = Compact || Variant is ButtonVariant.Ghost ? FontAttributes.None : FontAttributes.Bold;

        string background, foreground, stroke;
        if (!enabled)
        {
            (background, foreground, stroke) = ("SurfaceRaised", "InkMuted", "LineBrush");
        }
        else
        {
            (background, foreground, stroke) = Variant switch
            {
                ButtonVariant.Primary => ("Accent", "OnAccent", string.Empty),
                ButtonVariant.Ghost => (string.Empty, "InkMuted", string.Empty),
                ButtonVariant.Danger => ("Surface", "PCrit", "LineBrush"),
                _ => ("Surface", "Ink", "LineBrush"),
            };
        }

        // Прежнее значение фона (прозрачный у прозрачной кнопки, посветлевший у основной под указателем) мешало бы ресурсу.
        ClearValue(BackgroundColorProperty);

        var hovering = _hover && enabled;
        if (hovering && Variant is ButtonVariant.Secondary or ButtonVariant.Danger or ButtonVariant.Ghost)
        {
            background = "SurfaceRaised";
            if (Variant == ButtonVariant.Ghost)
            {
                foreground = "Ink";
            }
        }

        // Основная кнопка Aurora: градиент от акцента ко второму цвету темы; под указателем чуть светлее.
        Opacity = 1;
        if (enabled && Variant == ButtonVariant.Primary)
        {
            this.SetDynamicResource(BackgroundProperty, "AccentGradient");
            if (hovering)
            {
                Opacity = 0.92;
            }
        }
        else if (background.Length > 0)
        {
            this.SetDynamicResource(BackgroundColorProperty, background);
        }
        else
        {
            BackgroundColor = Colors.Transparent;
        }

        if (stroke.Length > 0)
        {
            this.SetDynamicResource(StrokeProperty, stroke);
        }
        else
        {
            Stroke = Colors.Transparent;
        }

        _label.SetDynamicResource(Label.TextColorProperty, foreground);
        _icon.SetDynamicResource(Controls.Icon.ColorProperty, foreground);
        _hint.SetDynamicResource(Label.TextColorProperty, foreground);
        _hintBox.SetDynamicResource(StrokeProperty, enabled && Variant == ButtonVariant.Primary ? "OnAccentBrush" : "LineBrush");

        // Свечение только у доступной основной кнопки.
        if (enabled && Variant == ButtonVariant.Primary)
        {
            var shadow = new Shadow { Radius = 22, Opacity = 0.35f, Offset = new Point(0, 0) };
            shadow.SetDynamicResource(Shadow.BrushProperty, "AccentBrush");
            Shadow = shadow;
        }
        else
        {
            Shadow = null!;
        }
    }
}
