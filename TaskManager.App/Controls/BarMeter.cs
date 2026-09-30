using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

/// <summary>
/// A horizontal indicator bar on a track: the fill takes the share <see cref="Fraction"/> (0-1) of the width.
/// Needed for statistics histograms, where the width depends on the available space, not on pixels.
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

    /// <summary>The share of the fill from 0 to 1.</summary>
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
