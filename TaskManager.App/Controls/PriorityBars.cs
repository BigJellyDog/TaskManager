using Microsoft.Maui.Graphics;

namespace TaskManager.App.Controls;

/// <summary>The priority icon: four bars, filled as many as the level (1 - low ... 4 - critical).</summary>
public sealed class PriorityBars : GraphicsView
{
    public static readonly BindableProperty LevelProperty =
        BindableProperty.Create(nameof(Level), typeof(int), typeof(PriorityBars), 1, propertyChanged: OnChanged);

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(PriorityBars), Colors.Gray, propertyChanged: OnChanged);

    private readonly BarsDrawable _drawable = new();

    public PriorityBars()
    {
        Drawable = _drawable;
        InputTransparent = true;
        WidthRequest = HeightRequest = 14;
        VerticalOptions = LayoutOptions.Center;
    }

    public int Level
    {
        get => (int)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var bars = (PriorityBars)bindable;
        bars._drawable.Level = bars.Level;
        bars._drawable.Color = bars.Color;
        bars.Invalidate();
    }

    private sealed class BarsDrawable : IDrawable
    {
        // x, y, height on the 16×16 grid, as in the mockup.
        private static readonly (float X, float Y, float Height)[] Bars = [(1, 11, 4), (5, 8, 7), (9, 5, 10), (13, 2, 13)];

        public int Level { get; set; } = 1;

        public Color Color { get; set; } = Colors.Gray;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            canvas.Scale(dirtyRect.Width / 16f, dirtyRect.Height / 16f);
            for (var i = 0; i < Bars.Length; i++)
            {
                canvas.FillColor = i < Level ? Color : Color.WithAlpha(0.28f);
                canvas.FillRoundedRectangle(Bars[i].X, Bars[i].Y, 3, Bars[i].Height, 1);
            }

            canvas.RestoreState();
        }
    }
}
