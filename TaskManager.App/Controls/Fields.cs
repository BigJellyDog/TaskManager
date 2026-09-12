using System.Collections;
using Microsoft.Maui.Controls.Shapes;

namespace TaskManager.App.Controls;

/// <summary>
/// Рамка поля ввода Signal: тонкая граница, а в фокусе сплошная обводка акцентом в 2 px. Внутри лежит Entry, Editor,
/// DatePicker или TimePicker без собственной рамки: фокус рамка узнаёт сама.
/// </summary>
public sealed class InputBox : Border
{
    public static readonly BindableProperty HasErrorProperty =
        BindableProperty.Create(nameof(HasError), typeof(bool), typeof(InputBox), false, propertyChanged: (b, _, _) => ((InputBox)b).Apply());

    private bool _focused;

    public InputBox()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 5 };
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
/// Выпадающее поле: показывает выбранный пункт и шеврон, а список открывает невидимый Picker поверх.
/// Используется там, где нужно поле высотой с обычный ввод (повторение, зависимость).
/// </summary>
public sealed class DropField : Grid
{
    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(DropField), string.Empty, propertyChanged: (b, _, _) => ((DropField)b).Apply());

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(DropField), null, propertyChanged: OnItemsChanged);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(DropField), null, BindingMode.TwoWay, propertyChanged: OnSelectedChanged);

    private readonly Picker _picker = new() { Opacity = 0.011, BackgroundColor = Colors.Transparent };
    private readonly Label _label = new() { FontSize = 14, VerticalOptions = LayoutOptions.Center };

    public DropField()
    {
        HeightRequest = 38;

        var chevron = new Icon { Kind = "chevron-down", Size = 14, HorizontalOptions = LayoutOptions.End };
        chevron.SetDynamicResource(Icon.ColorProperty, "InkMuted");

        var box = new InputBox { Content = new Grid { Children = { _label, chevron } } };
        _picker.SelectedIndexChanged += (_, _) =>
        {
            if (_picker.SelectedItem is { } item && !Equals(item, SelectedItem))
            {
                SelectedItem = item;
            }
        };

        Children.Add(box);
        Children.Add(_picker);
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
        field._picker.ItemsSource = newValue as IList;
        if (newValue is System.Collections.Specialized.INotifyCollectionChanged notify)
        {
            notify.CollectionChanged += (_, _) => field.Apply();
        }

        field.Apply();
    }

    private static void OnSelectedChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var field = (DropField)bindable;
        if (!Equals(field._picker.SelectedItem, newValue))
        {
            field._picker.SelectedItem = newValue;
        }

        field.Apply();
    }

    private void Apply()
    {
        var text = SelectedItem?.ToString();
        _label.Text = string.IsNullOrEmpty(text) ? Placeholder : text;
        _label.SetDynamicResource(Label.TextColorProperty, string.IsNullOrEmpty(text) ? "InkMuted" : "Ink");
    }
}
