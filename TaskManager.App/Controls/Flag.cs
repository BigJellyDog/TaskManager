namespace TaskManager.App.Controls;

/// <summary>
/// Флаг страны для кнопки языка. Эмодзи-флаги в Windows показываются буквами («DE»), поэтому флаги рисуются сами:
/// это прямоугольник 18×12 с тонкой рамкой.
/// </summary>
public sealed class Flag : GraphicsView, IDrawable
{
    public static readonly BindableProperty CodeProperty =
        BindableProperty.Create(nameof(Code), typeof(string), typeof(Flag), "ru", propertyChanged: (b, _, _) => ((Flag)b).Invalidate());

    public Flag()
    {
        Drawable = this;
        WidthRequest = 18;
        HeightRequest = 12;
        InputTransparent = true;
        VerticalOptions = LayoutOptions.Center;
    }

    /// <summary>Код языка: «de», «en», «ru», «ua».</summary>
    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var w = dirtyRect.Width;
        var h = dirtyRect.Height;
        canvas.SaveState();
        var clip = new PathF();
        clip.AppendRoundedRectangle(0, 0, w, h, 2);
        canvas.ClipPath(clip);

        switch (Code)
        {
            case "de":
                Stripes(canvas, w, h, "#000000", "#DD0000", "#FFCE00");
                break;
            case "ru":
                Stripes(canvas, w, h, "#FFFFFF", "#0039A6", "#D52B1E");
                break;
            case "ua":
                Fill(canvas, "#0057B7", 0, 0, w, h / 2);
                Fill(canvas, "#FFD700", 0, h / 2, w, h / 2);
                break;
            case "en":
                UnionJack(canvas, w, h);
                break;
        }

        canvas.RestoreState();

        // Тонкая рамка, чтобы белая полоса не сливалась со светлым фоном.
        canvas.StrokeColor = Color.FromRgba(128, 128, 128, 110);
        canvas.StrokeSize = 1;
        canvas.DrawRoundedRectangle(0.5f, 0.5f, w - 1, h - 1, 2);
    }

    private static void Fill(ICanvas canvas, string color, float x, float y, float w, float h)
    {
        canvas.FillColor = Color.FromArgb(color);
        canvas.FillRectangle(x, y, w, h);
    }

    private static void Stripes(ICanvas canvas, float w, float h, string top, string middle, string bottom)
    {
        var third = h / 3;
        Fill(canvas, top, 0, 0, w, third + 0.5f);
        Fill(canvas, middle, 0, third, w, third + 0.5f);
        Fill(canvas, bottom, 0, third * 2, w, third + 0.5f);
    }

    private static void UnionJack(ICanvas canvas, float w, float h)
    {
        Fill(canvas, "#012169", 0, 0, w, h);

        canvas.StrokeLineCap = LineCap.Butt;
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 2.6f;
        canvas.DrawLine(0, 0, w, h);
        canvas.DrawLine(0, h, w, 0);
        canvas.StrokeColor = Color.FromArgb("#C8102E");
        canvas.StrokeSize = 1.1f;
        canvas.DrawLine(0, 0, w, h);
        canvas.DrawLine(0, h, w, 0);

        Fill(canvas, "#FFFFFF", w / 2 - 2.2f, 0, 4.4f, h);
        Fill(canvas, "#FFFFFF", 0, h / 2 - 2.2f, w, 4.4f);
        Fill(canvas, "#C8102E", w / 2 - 1.2f, 0, 2.4f, h);
        Fill(canvas, "#C8102E", 0, h / 2 - 1.2f, w, 2.4f);
    }
}
