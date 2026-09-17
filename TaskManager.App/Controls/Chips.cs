using System.Collections;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

/// <summary>
/// Быстрый фильтр-чип «ПРОСРОЧЕНО · 2»: моноширинная подпись заглавными, рамка 1 px. Включённый чип подсвечивается
/// акцентом, «тревожный» (просрочено) красным.
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
        FontFamily = "Consolas",
        FontSize = 11,
        CharacterSpacing = 0.5,
        TextTransform = TextTransform.Uppercase,
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

        // Красный: просрочено (есть задачи). Акцент: фильтр включён. Иначе спокойная рамка.
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
/// Выпадающий чип «СТАТУС ▾» из макета: рамка 1 px, моноширинная подпись заглавными. Нажатие открывает список в стиле
/// Signal под чипом. Если выбран не первый пункт («Все …»), на чипе подпись выбранного, а рамка и текст акцентные.
/// </summary>
public sealed class DropChip : Border
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(DropChip), string.Empty, propertyChanged: OnChanged);

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(DropChip), null, propertyChanged: OnItemsChanged);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(DropChip), null, BindingMode.TwoWay, propertyChanged: OnChanged);

    private readonly Label _label = new()
    {
        FontFamily = "Consolas",
        FontSize = 11,
        CharacterSpacing = 0.5,
        TextTransform = TextTransform.Uppercase,
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

        var texts = items.Cast<object>().Select(x => x.ToString() ?? string.Empty).ToList();
        DropMenu.Show(this, texts, items.IndexOf(SelectedItem), index => SelectedItem = items[index], mono: true);
    }

    private void Apply()
    {
        var first = ItemsSource is { Count: > 0 } ? ItemsSource[0] : null;
        var chosen = SelectedItem is { } item && !Equals(item, first) ? item.ToString() : null;

        // В макете подпись «Тег ▾» просто текст со стрелкой.
        _label.Text = (chosen ?? Title) + " ▾";
        _label.SetDynamicResource(Label.TextColorProperty, chosen is null ? "InkMuted" : "Accent");
        this.SetDynamicResource(StrokeProperty, chosen is null ? "LineBrush" : "AccentBrush");
    }
}
