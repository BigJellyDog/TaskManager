using System.Collections;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

/// <summary>
/// The quick filter chip "OVERDUE · 2": a monospaced upper-case label, a 1 px border. An enabled chip is highlighted
/// with the accent, an "alarm" one (overdue) in red.
/// </summary>
public sealed class ToggleChip : Border
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(ToggleChip), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(ToggleChip), false, propertyChanged: OnChanged);

    public static readonly BindableProperty IsAlertProperty =
        BindableProperty.Create(nameof(IsAlert), typeof(bool), typeof(ToggleChip), false, propertyChanged: OnChanged);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ToggleChip));

    private readonly Label _label = new()
    {
        FontFamily = "Segoe UI Variable",
        FontSize = 11,
        CharacterSpacing = 0.5,
        TextTransform = TextTransform.None,
        VerticalOptions = LayoutOptions.Center,
    };

    public ToggleChip()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 4 };
        StrokeThickness = 1;
        Padding = new Thickness(10, 0);
        HeightRequest = 28;
        VerticalOptions = LayoutOptions.Center;
        Content = _label;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Command?.Execute(null);
        GestureRecognizers.Add(tap);

        Apply();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsAlert
    {
        get => (bool)GetValue(IsAlertProperty);
        set => SetValue(IsAlertProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((ToggleChip)bindable).Apply();

    private void Apply()
    {
        _label.Text = Text;

        // Red: overdue (there are tasks). Accent: the filter is on. Otherwise a calm border.
        var (text, stroke, background) = (IsAlert, IsActive) switch
        {
            (true, _) => ("PCrit", "PCritBrush", IsActive ? "SurfaceRaised" : "Bg"),
            (false, true) => ("Accent", "AccentBrush", "AccentSoft"),
            _ => ("InkMuted", "LineBrush", "Bg"),
        };

        _label.SetDynamicResource(Label.TextColorProperty, text);
        this.SetDynamicResource(StrokeProperty, stroke);
        this.SetDynamicResource(BackgroundColorProperty, background);
    }
}

/// <summary>
/// The drop-down chip "STATUS ▾" from the mockup: a 1 px border, a monospaced upper-case label. A tap opens a list in the Signal
/// style under the chip. If an item other than the first ("All ...") is chosen, the chip shows the chosen label, with an accent border and text.
/// </summary>
public sealed class DropChip : Border
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(DropChip), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(DropChip), null, propertyChanged: OnItemsChanged);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(DropChip), null, BindingMode.TwoWay, propertyChanged: OnChanged);

    public static readonly BindableProperty KindProperty =
        BindableProperty.Create(nameof(Kind), typeof(string), typeof(DropChip), string.Empty);

    private readonly Label _label = new()
    {
        FontFamily = "Segoe UI Variable",
        FontSize = 11,
        CharacterSpacing = 0.5,
        TextTransform = TextTransform.None,
        VerticalOptions = LayoutOptions.Center,
    };

    public DropChip()
    {
        VerticalOptions = LayoutOptions.Center;
        HeightRequest = 28;
        StrokeShape = new RoundRectangle { CornerRadius = 4 };
        StrokeThickness = 1;
        Padding = new Thickness(10, 0);
        Content = _label;
        this.SetDynamicResource(BackgroundColorProperty, "Bg");

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Open();
        GestureRecognizers.Add(tap);

        Apply();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The kind of items: "tag" (with a hash, like the tags on cards) or "priority" (bars and the priority colour).</summary>
    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
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

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue) => ((DropChip)bindable).Apply();

    // The "All ..." item is always first and has no styling; the others are styled by the kind of list.
    private DropEntry Describe(object item, int index)
    {
        var text = item.ToString() ?? string.Empty;
        if (index == 0)
        {
            return new DropEntry(text);
        }

        if (Kind == "tag")
        {
            return new DropEntry("#" + text, Muted: true);
        }

        if (Kind == "priority" && item.GetType().GetProperty("Value")?.GetValue(item) is Core.Domain.Priority priority)
        {
            var key = priority switch
            {
                Core.Domain.Priority.Critical => "PCrit",
                Core.Domain.Priority.High => "PHigh",
                Core.Domain.Priority.Medium => "PMed",
                _ => "PLow",
            };
            return new DropEntry(text, (int)priority + 1, key);
        }

        return new DropEntry(text);
    }

    private static void OnItemsChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var chip = (DropChip)bindable;
        if (newValue is System.Collections.Specialized.INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += (_, _) => chip.Apply();
        }

        chip.Apply();
    }

    private void Open()
    {
        if (ItemsSource is not { Count: > 0 } items)
        {
            return;
        }

        var entries = items.Cast<object>().Select(Describe).ToList();
        DropMenu.Show(this, entries, items.IndexOf(SelectedItem), index => SelectedItem = items[index], mono: true);
    }

    private void Apply()
    {
        var first = ItemsSource is { Count: > 0 } ? ItemsSource[0] : null;
        var chosen = SelectedItem is { } item && !Equals(item, first) ? item.ToString() : null;

        // In the mockup the "Tag ▾" label is just text with an arrow.
        var shown = chosen is not null && Kind == "tag" ? "#" + chosen : chosen;
        _label.Text = (shown ?? Title) + " ▾";
        _label.SetDynamicResource(Label.TextColorProperty, chosen is null ? "InkMuted" : "Accent");
        this.SetDynamicResource(StrokeProperty, chosen is null ? "LineBrush" : "AccentBrush");
    }
}
