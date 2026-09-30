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
/// The Signal button: an icon, a label and an optional shortcut hint. The primary one (lime) with a glow,
/// the normal one with a border, the transparent one and the dangerous one (red text). A disabled button stays in place but grey.
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

        // We colour the labels inside the button ourselves: the general Label style recolours a disabled label grey on top of any colour.
        _label.Style = new Style(typeof(Label));
        _hint.Style = new Style(typeof(Label));

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (IsEnabled && Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        };
        GestureRecognizers.Add(tap);

        // Under the pointer: the normal and dangerous buttons lighten to the "raised" surface, the transparent one gets the same
        // background and bright text, the primary one becomes slightly brighter.
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

    /// <summary>The icon is on the right of the label.</summary>
    public bool IconAfter
    {
        get => (bool)GetValue(IconAfterProperty);
        set => SetValue(IconAfterProperty, value);
    }

    /// <summary>A small button (height 28, a label of normal weight).</summary>
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

        // A binding to a model that is not filled yet may return null: then we treat the text as empty.
        var text = Text ?? string.Empty;
        var icon = Icon ?? string.Empty;
        var hint = Hint ?? string.Empty;

        _label.Text = text;
        _label.IsVisible = text.Length > 0;
        _icon.Kind = icon;
        _icon.IsVisible = icon.Length > 0;
        _hint.Text = hint;
        _hintBox.IsVisible = hint.Length > 0;
        // A height set in XAML (for example 42 on the main card button) is not overwritten: we apply the standard 28/32 ourselves.
        if (HeightRequest is -1 or 28 or 32 or 36)
        {
            HeightRequest = Compact ? 28 : 36;
        }

        Padding = Compact ? new Thickness(12, 0) : new Thickness(16, 0);
        _label.FontAttributes = Compact || Variant is ButtonVariant.Ghost ? FontAttributes.None : FontAttributes.Bold;

        string background, foreground, stroke;
        if (!enabled && Variant == ButtonVariant.Primary)
        {
            // An unavailable primary button stays recognisable: the same gradient with the same dark (or white) text, but paler.
            (background, foreground, stroke) = ("Accent", "OnAccent", string.Empty);
        }
        else if (!enabled)
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

        // The previous background would interfere with the new one. The primary button gradient lives in Background, ordinary colours in BackgroundColor, and the resource
        // of one property is not removed by clearing the other: an unavailable button would stay filled with the gradient (with grey
        // text on a light fill).
        RemoveDynamicResource(BackgroundProperty);
        RemoveDynamicResource(BackgroundColorProperty);
        ClearValue(BackgroundProperty);
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

        // The Aurora primary button: a gradient from the accent to the second colour of the theme; slightly lighter under the pointer.
        Opacity = 1;
        if (Variant == ButtonVariant.Primary)
        {
            this.SetDynamicResource(BackgroundProperty, "AccentGradient");
            Opacity = !enabled ? 0.5 : hovering ? 0.92 : 1;
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

        // A glow only on an available primary button.
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
