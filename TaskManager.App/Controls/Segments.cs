using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

/// <summary>Ряд из <see cref="Count"/> коротких делений, первые <see cref="Filled"/> залиты акцентом (индикатор «Вовремя»).</summary>
public sealed class Segments : Grid
{
    public static readonly BindableProperty CountProperty =
        BindableProperty.Create(nameof(Count), typeof(int), typeof(Segments), 5, propertyChanged: OnChanged);

    public static readonly BindableProperty FilledProperty =
        BindableProperty.Create(nameof(Filled), typeof(int), typeof(Segments), 0, propertyChanged: OnChanged);

    public Segments()
    {
        ColumnSpacing = 3;
        HeightRequest = 5;
        Rebuild();
    }

    public new int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public int Filled
    {
        get => (int)GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((Segments)bindable).Rebuild();

    private void Rebuild()
    {
        Children.Clear();
        ColumnDefinitions.Clear();

        for (var i = 0; i < Count; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var block = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 1 } };
            block.SetDynamicResource(BackgroundColorProperty, i < Filled ? "Accent" : "Line");
            this.Add(block, i, 0);
        }
    }
}
