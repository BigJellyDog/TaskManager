using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

/// <summary>
/// Переключатель Signal 36×20: пустая рамка с кружком слева, во включённом виде заливка акцентом и кружок справа.
/// Работает как флажок: нажатие меняет <see cref="IsOn"/>, значение можно привязать в обе стороны.
/// </summary>
public sealed class SwitchToggle : Border
{
    public static readonly BindableProperty IsOnProperty =
        BindableProperty.Create(nameof(IsOn), typeof(bool), typeof(SwitchToggle), false, BindingMode.TwoWay, propertyChanged: OnChanged);

    public static readonly BindableProperty ToggledCommandProperty =
        BindableProperty.Create(nameof(ToggledCommand), typeof(ICommand), typeof(SwitchToggle));

    private readonly Border _thumb = new()
    {
        WidthRequest = 14,
        HeightRequest = 14,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 7 },
        VerticalOptions = LayoutOptions.Center,
    };

    public SwitchToggle()
    {
        WidthRequest = 36;
        HeightRequest = 20;
        Padding = new Thickness(2);
        StrokeThickness = 1;
        StrokeShape = new RoundRectangle { CornerRadius = 10 };
        HorizontalOptions = LayoutOptions.End;
        VerticalOptions = LayoutOptions.Center;
        Content = _thumb;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            IsOn = !IsOn;
            ToggledCommand?.Execute(IsOn);
        };
        GestureRecognizers.Add(tap);

        Apply();
    }

    public bool IsOn
    {
        get => (bool)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    /// <summary>Команда, которую вызывает нажатие; параметр — новое значение.</summary>
    public ICommand? ToggledCommand
    {
        get => (ICommand?)GetValue(ToggledCommandProperty);
        set => SetValue(ToggledCommandProperty, value);
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((SwitchToggle)bindable).Apply();

    private void Apply()
    {
        _thumb.HorizontalOptions = IsOn ? LayoutOptions.End : LayoutOptions.Start;
        this.SetDynamicResource(BackgroundColorProperty, IsOn ? "Accent" : "Bg");
        this.SetDynamicResource(StrokeProperty, IsOn ? "AccentBrush" : "InkMutedBrush");
        _thumb.SetDynamicResource(BackgroundColorProperty, IsOn ? "OnAccent" : "InkMuted");
    }
}
