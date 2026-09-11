using Microsoft.Maui.Graphics;

namespace TaskManager.App.Controls;

/// <summary>
/// Линейная иконка из набора Signal (сетка 24×24, штрих 1.8, скруглённые концы). Рисуется по тем же путям, что и в макете,
/// цвет задаётся свойством <see cref="Color"/> и перекрашивается вместе с темой.
/// </summary>
public sealed class Icon : GraphicsView
{
    public static readonly BindableProperty KindProperty =
        BindableProperty.Create(nameof(Kind), typeof(string), typeof(Icon), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(Icon), Colors.Gray, propertyChanged: OnChanged);

    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(Icon), 18d, propertyChanged: OnSizeChanged);

    private readonly IconDrawable _drawable = new();

    public Icon()
    {
        Drawable = _drawable;
        InputTransparent = true;
        WidthRequest = HeightRequest = 18;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Center;
    }

    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var icon = (Icon)bindable;
        icon._drawable.Kind = icon.Kind;
        icon._drawable.Color = icon.Color;
        icon.Invalidate();
    }

    private static void OnSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var icon = (Icon)bindable;
        icon.WidthRequest = icon.HeightRequest = icon.Size;
        icon.Invalidate();
    }

    private sealed class IconDrawable : IDrawable
    {
        private static readonly Dictionary<string, PathF> Cache = [];

        public string Kind { get; set; } = string.Empty;

        public Color Color { get; set; } = Colors.Gray;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (!IconPaths.All.TryGetValue(Kind, out var data))
            {
                return;
            }

            canvas.SaveState();
            canvas.Scale(dirtyRect.Width / 24f, dirtyRect.Height / 24f);
            canvas.StrokeColor = Color;
            canvas.StrokeSize = 1.8f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            foreach (var (path, filled) in data)
            {
                if (!Cache.TryGetValue(path, out var geometry))
                {
                    Cache[path] = geometry = PathBuilder.Build(path);
                }

                if (filled)
                {
                    canvas.FillColor = Color;
                    canvas.FillPath(geometry);
                }
                else
                {
                    canvas.DrawPath(geometry);
                }
            }

            canvas.RestoreState();
        }
    }
}

/// <summary>Пути иконок (viewBox 24×24) из макета Signal.</summary>
internal static class IconPaths
{
    public static IReadOnlyDictionary<string, (string Path, bool Filled)[]> All { get; } =
        new Dictionary<string, (string, bool)[]>
        {
            ["calendar"] = [("M4 6h16v14H4zM4 10h16M8 3v4M16 3v4", false)],
            ["trash"] = [("M5 7h14M10 7V4h4v3M7 7l1 13h8l1-13", false)],
            ["edit"] = [("M4 20l4-1 11-11-3-3L5 16zM14 6l3 3", false)],
            ["blocked"] = [("M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM6.3 6.3l11.4 11.4", false)],
            ["check"] = [("M5 12.5l4.5 4.5L19 7.5", false)],
            ["circle"] = [("M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16z", false)],
            ["half"] = [("M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16z", false), ("M12 4a8 8 0 0 1 0 16z", true)],
            ["subtask"] = [("M6 4v8a4 4 0 0 0 4 4h9M15 12l4 4-4 4", false)],
            ["done"] = [("M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM8.5 12.5l2.5 2.5 4.5-5", false)],
            ["chevron-down"] = [("M6 9l6 6 6-6", false)],
            ["list"] = [("M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01", false)],
            ["bell"] = [("M6 16v-5a6 6 0 0 1 12 0v5l2 2H4zM10 20a2 2 0 0 0 4 0", false)],
            ["plus"] = [("M12 5v14M5 12h14", false)],
            ["chevron-left"] = [("M15 5l-7 7 7 7", false)],
            ["chevron-right"] = [("M9 5l7 7-7 7", false)],
            ["search"] = [("M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM20 20l-4-4", false)],
            ["undo"] = [("M9 8L4 13l5 5M4 13h11a5 5 0 0 0 0-10h-3", false)],
            ["cancelled"] = [("M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16zM9 9l6 6M15 9l-6 6", false)],
            ["chart"] = [("M5 20V10M12 20V4M19 20v-7", false)],
            ["database"] = [("M4 6c0-1.7 3.6-3 8-3s8 1.3 8 3-3.6 3-8 3-8-1.3-8-3zM4 6v6c0 1.7 3.6 3 8 3s8-1.3 8-3V6M4 12v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6", false)],
            ["board"] = [("M4 5h4v14H4zM10 5h4v9h-4zM16 5h4v6h-4z", false)],
            ["refresh"] = [("M20 11a8 8 0 0 0-14-4L4 9M4 4v5h5M4 13a8 8 0 0 0 14 4l2-2M20 20v-5h-5", false)],
            ["link"] = [("M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1", false)],
            ["x"] = [("M6 6l12 12M18 6L6 18", false)],
            ["tag"] = [("M3 12V4h8l10 10-8 8zM7.5 8h.01", false)],
            ["settings"] = [("M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6zM12 2v3M12 19v3M2 12h3M19 12h3M5 5l2 2M17 17l2 2M19 5l-2 2M7 17l-2 2", false)],
            ["repeat"] = [("M17 3l3 3-3 3M4 11V9a3 3 0 0 1 3-3h13M7 21l-3-3 3-3M20 13v2a3 3 0 0 1-3 3H4", false)],
        };
}
