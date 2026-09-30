using System.Collections;
using System.Collections.Specialized;
using Microsoft.Maui.Graphics;
using TaskManager.App.Services;
using TaskManager.App.ViewModels;

namespace TaskManager.App.Controls;

/// <summary>
/// A bar chart by day: for every day a track and a fill from the bottom, the number above the bar and a label below it.
/// It is drawn as one canvas, so the bars are even for any number of days (7 or 30), and labels wider than a bar
/// are simply centred on it and do not push the neighbours.
/// </summary>
public sealed class DayChart : GraphicsView
{
    public static readonly BindableProperty ColumnsProperty =
        BindableProperty.Create(nameof(Columns), typeof(IEnumerable), typeof(DayChart), null, propertyChanged: OnColumnsChanged);

    private readonly ChartPainter _painter = new();

    public DayChart()
    {
        // Number on top 16 + gap 4 + track 78 + gap 4 + label 18.
        HeightRequest = 120;
        Drawable = _painter;
        ThemeManager.Changed += OnThemeChanged;
    }

    /// <summary>The chart columns (<see cref="ChartColumnViewModel"/>); redrawn on any change of the set.</summary>
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

        public List<ChartColumnViewModel> Columns { get; set; } = [];

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
            var mono = new Microsoft.Maui.Graphics.Font("Segoe UI Variable", 700, FontStyleType.Normal);
            var normal = new Microsoft.Maui.Graphics.Font("Segoe UI Variable");

            var width = dirtyRect.Width;
            var columnWidth = width / n;

            // Narrow bars (month) almost touching, wide ones (week) with some air, but not wider than reasonable.
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
