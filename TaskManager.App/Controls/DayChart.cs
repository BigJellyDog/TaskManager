using System.Collections;
using System.Collections.Specialized;
using Microsoft.Maui.Graphics;
using TaskManager.App.Services;
using TaskManager.App.ViewModels;

namespace TaskManager.App.Controls;

/// <summary>
/// Столбчатый график по дням: на каждый день подложка и заливка снизу, число над столбцом, подпись под ним.
/// Рисуется целиком одним полотном, поэтому столбцы ровные при любом числе дней (7 или 30), а подписи шире столбца
/// просто центрируются по нему и не сдвигают соседей.
/// </summary>
public sealed class DayChart : GraphicsView
{
    public static readonly BindableProperty ColumnsProperty =
        BindableProperty.Create(nameof(Columns), typeof(IEnumerable), typeof(DayChart), null, propertyChanged: OnColumnsChanged);

    private readonly ChartPainter _painter = new();

    public DayChart()
    {
        // Число сверху 16 + зазор 4 + дорожка 78 + зазор 4 + подпись 18.
        HeightRequest = 120;
        Drawable = _painter;
        ThemeManager.Changed += OnThemeChanged;
    }

    /// <summary>Столбцы графика (<see cref="ChartColumnViewModel"/>); перерисовка при любом изменении набора.</summary>
    public IEnumerable? Columns
    {
        get => (IEnumerable?)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    private static void OnColumnsChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var chart = (DayChart)bindable;
        if (oldValue is INotifyCollectionChanged oldNotify)
        {
            oldNotify.CollectionChanged -= chart.OnCollectionChanged;
        }

        if (newValue is INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += chart.OnCollectionChanged;
        }

        chart._painter.Columns = (newValue as IEnumerable)?.Cast<ChartColumnViewModel>().ToList() ?? [];
        chart.Invalidate();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _painter.Columns = Columns?.Cast<ChartColumnViewModel>().ToList() ?? [];
        Invalidate();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

    private sealed class ChartPainter : IDrawable
    {
        private const float ValueHeight = 16;
        private const float TrackTop = 20;
        private const float TrackHeight = 78;
        private const float LabelTop = 102;
        private const float LabelHeight = 18;

        public IReadOnlyList<ChartColumnViewModel> Columns { get; set; } = [];

        private static Color Token(string key, Color fallback) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : fallback;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var n = Columns.Count;
            if (n == 0)
            {
                return;
            }

            var track = Token("SurfaceRaised", Colors.LightGray);
            var ink = Token("Ink", Colors.Black);
            var muted = Token("InkMuted", Colors.Gray);
            var mono = new Microsoft.Maui.Graphics.Font("Consolas", 700, FontStyleType.Normal);
            var normal = new Microsoft.Maui.Graphics.Font("Segoe UI Variable");

            var width = dirtyRect.Width;
            var columnWidth = width / n;

            // Узкие столбцы (месяц) почти вплотную, широкие (неделя) с воздухом, но не шире разумного.
            var dense = n > 12;
            var barWidth = (float)Math.Clamp(columnWidth - (dense ? 2 : 8), 2, 56);

            for (var i = 0; i < n; i++)
            {
                var column = Columns[i];
                var centre = (columnWidth * i) + (columnWidth / 2);
                var left = centre - (barWidth / 2);
                var radius = Math.Min(3f, barWidth / 2);

                canvas.FillColor = track;
                canvas.FillRoundedRectangle(left, TrackTop, barWidth, TrackHeight, radius);

                var fill = (float)Math.Clamp(column.Height, 0, TrackHeight);
                if (fill > 0)
                {
                    canvas.FillColor = column.Color;
                    canvas.FillRoundedRectangle(left, TrackTop + TrackHeight - fill, barWidth, fill, Math.Min(radius, fill / 2));
                }

                var textWidth = dense ? 44f : columnWidth;

                if (column.Value.Length > 0)
                {
                    canvas.Font = mono;
                    canvas.FontSize = 12;
                    canvas.FontColor = ink;
                    canvas.DrawString(column.Value, centre - (textWidth / 2), 0, textWidth, ValueHeight, HorizontalAlignment.Center, VerticalAlignment.Center);
                }

                if (column.Label.Length > 0)
                {
                    canvas.Font = normal;
                    canvas.FontSize = dense ? 10 : 11;
                    canvas.FontColor = muted;
                    canvas.DrawString(column.Label, centre - (textWidth / 2), LabelTop, textWidth, LabelHeight, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
            }
        }
    }
}
