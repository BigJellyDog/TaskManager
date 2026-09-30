using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

/// <summary>
/// The Signal segmented choice ("7 days | 30 days | All time"): a rounded track, the selected item lighter and bolder.
/// The items are given as a string separated by "|", the selected number is bound in both directions.
/// </summary>
public sealed class SegmentedChoice : Border
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(string), typeof(SegmentedChoice), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(SegmentedChoice), 0, BindingMode.TwoWay, propertyChanged: OnChanged);

    private readonly HorizontalStackLayout _row = new() { Spacing = 2 };

    public SegmentedChoice()
    {
        StrokeThickness = 0;
        StrokeShape = new RoundRectangle { CornerRadius = 18 };
        Padding = new Thickness(3);
        HorizontalOptions = LayoutOptions.End;
        VerticalOptions = LayoutOptions.Center;
        this.SetDynamicResource(BackgroundColorProperty, "SurfaceRaised");
        Content = _row;
    }

    /// <summary>The item labels separated by "|".</summary>
    public string Items
    {
        get => (string)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((SegmentedChoice)bindable).Rebuild();

    private void Rebuild()
    {
        _row.Children.Clear();
        var titles = Items.Split('|', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < titles.Length; i++)
        {
            var index = i;
            var selected = index == SelectedIndex;

            var label = new Label
            {
                Text = titles[i],
                FontSize = 13,
                FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                VerticalOptions = LayoutOptions.Center,
            };
            label.SetDynamicResource(Label.TextColorProperty, selected ? "Ink" : "InkMuted");

            var item = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(12, 4),
                Content = label,
            };

            if (selected)
            {
                item.SetDynamicResource(BackgroundColorProperty, "Surface");
            }
            else
            {
                item.BackgroundColor = Colors.Transparent;
            }

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => SelectedIndex = index;
            item.GestureRecognizers.Add(tap);
            _row.Children.Add(item);
        }
    }
}
