using Microsoft.Maui.Graphics;

namespace TaskManager.App.Controls;

/// <summary>
/// Линейная иконка из набора Signal (сетка 24×24, штрих 1.8, скруглённые концы). Пути взяты из набора Lucide (ISC),
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
            // Толщину штриха задаём в экранных пикселях, а не в долях сетки 24×24: на мелких размерах 2/24 даёт
            // 1,2-1,3 px, и линии размазываются в серое. Здесь они держат 1,5-2 px.
            var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
            var pixels = size <= 14.5f ? 1.5f : size <= 16.5f ? 1.6f : size <= 18.5f ? 1.75f : 2f;
            canvas.StrokeSize = pixels * 24f / size;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            foreach (var (path, filled) in data)
            {
                if (!Cache.TryGetValue(path, out var geometry))
                {
                    Cache[path] = geometry = PathBuilder.Build(SvgPathNormalizer.Normalize(path) ?? string.Empty);
                }

                try
                {
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
                catch (ArgumentException)
                {
                    // Win2D отвергает некорректный путь: одна плохая иконка не должна ронять окно.
                }
            }

            canvas.RestoreState();
        }
    }
}

/// <summary>Пути иконок (viewBox 24x24) из набора Lucide (https://lucide.dev, лицензия ISC, lucide-static 0.469.0).</summary>
internal static class IconPaths
{
    public static IReadOnlyDictionary<string, (string Path, bool Filled)[]> All { get; } =
        new Dictionary<string, (string, bool)[]>
        {
            ["calendar"] = [("M8 2v4", false), ("M16 2v4", false), ("M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z", false), ("M3 10h18", false)], // calendar
            ["trash"] = [("M3 6h18", false), ("M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6", false), ("M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2", false), ("M10 11L10 17", false), ("M14 11L14 17", false)], // trash-2
            ["edit"] = [("M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z", false), ("m15 5 4 4", false)], // pencil
            ["blocked"] = [("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0", false), ("m4.9 4.9 14.2 14.2", false)], // ban
            ["check"] = [("M20 6 9 17l-5-5", false)], // check
            ["circle"] = [("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0", false)], // circle
            ["half"] = [("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0", false), ("M12 18a6 6 0 0 0 0-12v12z", true)], // contrast
            ["subtask"] = [("M15 10L20 15L15 20", false), ("M4 4v7a4 4 0 0 0 4 4h12", false)], // corner-down-right
            ["done"] = [("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0", false), ("m9 12 2 2 4-4", false)], // circle-check
            ["chevron-down"] = [("m6 9 6 6 6-6", false)], // chevron-down
            ["list"] = [("M9 6h12", false), ("M9 12h12", false), ("M9 18h12", false), ("M2.5 6a1.5 1.5 0 1 0 3 0a1.5 1.5 0 1 0 -3 0", true), ("M2.5 12a1.5 1.5 0 1 0 3 0a1.5 1.5 0 1 0 -3 0", true), ("M2.5 18a1.5 1.5 0 1 0 3 0a1.5 1.5 0 1 0 -3 0", true)], // list
            ["bell"] = [("M10.268 21a2 2 0 0 0 3.464 0", false), ("M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326", false)], // bell
            ["plus"] = [("M5 12h14", false), ("M12 5v14", false)], // plus
            ["chevron-left"] = [("m15 18-6-6 6-6", false)], // chevron-left
            ["chevron-right"] = [("m9 18 6-6-6-6", false)], // chevron-right
            ["search"] = [("M3 11a8 8 0 1 0 16 0a8 8 0 1 0 -16 0", false), ("m21 21-4.3-4.3", false)], // search
            ["undo"] = [("M9 14 4 9l5-5", false), ("M4 9h10.5a5.5 5.5 0 0 1 5.5 5.5a5.5 5.5 0 0 1-5.5 5.5H11", false)], // undo-2
            ["cancelled"] = [("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0", false), ("m15 9-6 6", false), ("m9 9 6 6", false)], // circle-x
            ["chart"] = [("M18 20L18 10", false), ("M12 20L12 4", false), ("M6 20L6 14", false)], // chart-no-axes-column
            ["database"] = [("M3 5a9 3 0 1 0 18 0a9 3 0 1 0 -18 0", false), ("M3 5V19A9 3 0 0 0 21 19V5", false), ("M3 12A9 3 0 0 0 21 12", false)], // database
            ["globe"] = [("M2 12a10 10 0 1 0 20 0a10 10 0 1 0 -20 0", false), ("M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20", false), ("M2 12h20", false)], // globe
            ["board"] = [("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2h-14a2 2 0 0 1 -2 -2v-14a2 2 0 0 1 2 -2z", false), ("M9 3v18", false), ("M15 3v18", false)], // columns-3: доска из колонок
            ["refresh"] = [("M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8", false), ("M21 3v5h-5", false), ("M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16", false), ("M8 16H3v5", false)], // refresh-cw
            ["link"] = [("M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71", false), ("M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71", false)], // link
            ["x"] = [("M18 6 6 18", false), ("m6 6 12 12", false)], // x
            ["tag"] = [("M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z", false), ("M7 7.5a0.5 0.5 0 1 0 1 0a0.5 0.5 0 1 0 -1 0", false)], // tag
            ["settings"] = [("M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z", false), ("M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0", false)], // settings
            ["repeat"] = [("m17 2 4 4-4 4", false), ("M3 11v-1a4 4 0 0 1 4-4h14", false), ("m7 22-4-4 4-4", false), ("M21 13v1a4 4 0 0 1-4 4H3", false)], // repeat
        };
}
