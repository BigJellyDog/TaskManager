using System.Collections;
using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

/// <summary>
/// The Signal input frame: a thin border, and when focused a solid 2 px accent outline. Inside there is an Entry, Editor,
/// DatePicker or TimePicker without a border of its own: the frame learns about focus by itself.
/// </summary>
public sealed class InputBox : Border
{
    public static readonly BindableProperty HasErrorProperty =
        BindableProperty.Create(nameof(HasError), typeof(bool), typeof(InputBox), false, propertyChanged: (b, _, _) => ((InputBox)b).Apply());

    private bool _focused;

    public InputBox()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 12 };
        StrokeThickness = 1;
        Padding = new Thickness(10, 0);
        this.SetDynamicResource(BackgroundColorProperty, "Surface");
        Apply();
    }

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    protected override void OnChildAdded(Element child)
    {
        base.OnChildAdded(child);

        switch (child)
        {
            case Entry entry:
                entry.Focused += (_, _) => SetFocus(true);
                entry.Unfocused += (_, _) => SetFocus(false);
                break;
            case Editor editor:
                editor.Focused += (_, _) => SetFocus(true);
                editor.Unfocused += (_, _) => SetFocus(false);
                break;
        }
    }

    private void SetFocus(bool focused)
    {
        _focused = focused;
        Apply();
    }

    private void Apply()
    {
        this.SetDynamicResource(StrokeProperty, HasError ? "PCritBrush" : _focused ? "AccentBrush" : "LineBrush");
        StrokeThickness = _focused || HasError ? 2 : 1;
    }
}

/// <summary>
/// The drop-down field: shows the chosen item and a chevron, a tap opens a Signal-style list under the field.
/// Used where a field of the height of a normal input is needed (repeat, dependency).
/// </summary>
public sealed class DropField : Grid
{
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(DropField), string.Empty, propertyChanged: (b, _, _) => ((DropField)b).Apply());

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(DropField), null, propertyChanged: OnItemsChanged);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(DropField), null, BindingMode.TwoWay, propertyChanged: (b, _, _) => ((DropField)b).Apply());

    private readonly Label _label = new() { FontSize = 14, VerticalOptions = LayoutOptions.Center };
    private readonly InputBox _box;

    /// <summary>Colour samples for the list items (for the theme choice): three colours per item, in the same order.</summary>
    public static readonly BindableProperty SwatchesProperty =
        BindableProperty.Create(nameof(Swatches), typeof(IList<string[]>), typeof(DropField), null);

    public IList<string[]>? Swatches
    {
        get => (IList<string[]>?)GetValue(SwatchesProperty);
        set => SetValue(SwatchesProperty, value);
    }

    public DropField()
    {
        HeightRequest = 38;

        var chevron = new Icon { Kind = "chevron-down", Size = 14, HorizontalOptions = LayoutOptions.End };
        chevron.SetDynamicResource(Icon.ColorProperty, "InkMuted");

        _box = new InputBox { Content = new Grid { Children = { _label, chevron } } };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Open();
        _box.GestureRecognizers.Add(tap);

        Children.Add(_box);
        Apply();
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private static void OnItemsChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var field = (DropField)bindable;
        if (newValue is System.Collections.Specialized.INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += (_, _) => field.Apply();
        }

        field.Apply();
    }

    private void Open()
    {
        if (ItemsSource is not { Count: > 0 } items)
        {
            return;
        }

        var entries = items.Cast<object>()
            .Select((x, i) => new DropEntry(x.ToString() ?? string.Empty, Swatch: Swatches is { } s && i < s.Count ? s[i] : null))
            .ToList();
        DropMenu.Show(_box, entries, items.IndexOf(SelectedItem), index => SelectedItem = items[index], mono: false);
    }

    private void Apply()
    {
        var text = SelectedItem?.ToString();
        _label.Text = string.IsNullOrEmpty(text) ? Placeholder : text;
        _label.SetDynamicResource(Label.TextColorProperty, string.IsNullOrEmpty(text) ? "InkMuted" : "Ink");
    }
}
