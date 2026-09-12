using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

/// <summary>
/// Горизонтальная полоса-индикатор на подложке: заливка занимает долю <see cref="Fraction"/> (0–1) от ширины.
/// Нужна для гистограмм статистики, где ширина зависит от доступного места, а не от пикселей.
/// </summary>
public sealed class BarMeter : Border
{
    public static readonly BindableProperty FractionProperty =
        BindableProperty.Create(nameof(Fraction), typeof(double), typeof(BarMeter), 0d, propertyChanged: OnFractionChanged);

    public static readonly BindableProperty FillColorProperty =
        BindableProperty.Create(nameof(FillColor), typeof(Color), typeof(BarMeter), Colors.Gray, propertyChanged: OnFillChanged);

    private readonly Grid _grid = new() { ColumnSpacing = 0 };
    private readonly BoxView _fill = new() { Color = Colors.Gray };

    public BarMeter()
    {
        StrokeThickness = 0;
        StrokeShape = new RoundRectangle { CornerRadius = 3 };
        HeightRequest = 22;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Fill;
        this.SetDynamicResource(BackgroundColorProperty, "SurfaceRaised");

        _grid.Add(_fill, 0, 0);
        Content = _grid;
        ApplyFraction();
    }

    /// <summary>Доля заливки от 0 до 1.</summary>
    public double Fraction
    {
        get => (double)GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public Color FillColor
    {
        get => (Color)GetValue(FillColorProperty);
        set => SetValue(FillColorProperty, value);
    }

    private static void OnFractionChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((BarMeter)bindable).ApplyFraction();

    private static void OnFillChanged(BindableObject bindable, object oldValue, object newValue) =>
        ((BarMeter)bindable)._fill.Color = (Color)newValue;

    private void ApplyFraction()
    {
        var fraction = Math.Clamp(Fraction, 0, 1);
        _grid.ColumnDefinitions =
        [
            new ColumnDefinition(new GridLength(fraction, GridUnitType.Star)),
            new ColumnDefinition(new GridLength(1 - fraction, GridUnitType.Star)),
        ];
    }
}
