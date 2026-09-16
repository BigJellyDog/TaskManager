namespace TaskManager.App.Controls;

/// <summary>
/// Подсветка элемента под указателем мыши, как в макете Signal: пункты меню и строки списка получают фон «приподнятой»
/// поверхности, карточки на доске обводку акцентом. Цвета задаются ключами ресурсов, поэтому подсветка следует за темой.
/// Если <see cref="IsSuppressed"/> истинно (например, пункт уже выбран или карточка просрочена), подсветка не включается.
/// </summary>
public sealed class HoverBehavior : Behavior<View>
{
    public static readonly BindableProperty HoverBackgroundProperty =
        BindableProperty.Create(nameof(HoverBackground), typeof(string), typeof(HoverBehavior), string.Empty);

    public static readonly BindableProperty HoverStrokeProperty =
        BindableProperty.Create(nameof(HoverStroke), typeof(string), typeof(HoverBehavior), string.Empty);

    public static readonly BindableProperty RestStrokeProperty =
        BindableProperty.Create(nameof(RestStroke), typeof(string), typeof(HoverBehavior), "LineBrush");

    public static readonly BindableProperty IsSuppressedProperty =
        BindableProperty.Create(nameof(IsSuppressed), typeof(bool), typeof(HoverBehavior), false, propertyChanged: OnSuppressedChanged);

    private View? _view;
    private PointerGestureRecognizer? _pointer;
    private bool _hovering;

    /// <summary>Ключ цвета фона под указателем; пусто, если фон не меняется.</summary>
    public string HoverBackground
    {
        get => (string)GetValue(HoverBackgroundProperty);
        set => SetValue(HoverBackgroundProperty, value);
    }

    /// <summary>Ключ кисти обводки под указателем (только для Border); пусто, если обводка не меняется.</summary>
    public string HoverStroke
    {
        get => (string)GetValue(HoverStrokeProperty);
        set => SetValue(HoverStrokeProperty, value);
    }

    /// <summary>Ключ кисти обводки в покое.</summary>
    public string RestStroke
    {
        get => (string)GetValue(RestStrokeProperty);
        set => SetValue(RestStrokeProperty, value);
    }

    public bool IsSuppressed
    {
        get => (bool)GetValue(IsSuppressedProperty);
        set => SetValue(IsSuppressedProperty, value);
    }

    protected override void OnAttachedTo(View bindable)
    {
        base.OnAttachedTo(bindable);
        _view = bindable;

        _pointer = new PointerGestureRecognizer();
        _pointer.PointerEntered += (_, _) => Set(true);
        _pointer.PointerExited += (_, _) => Set(false);
        bindable.GestureRecognizers.Add(_pointer);

        // Поведение подключается позже, чем создаётся элемент, и наследует его контекст привязки.
        BindingContext = bindable.BindingContext;
        bindable.BindingContextChanged += OnViewContextChanged;
    }

    protected override void OnDetachingFrom(View bindable)
    {
        bindable.BindingContextChanged -= OnViewContextChanged;
        if (_pointer is not null)
        {
            bindable.GestureRecognizers.Remove(_pointer);
        }

        _view = null;
        base.OnDetachingFrom(bindable);
    }

    private void OnViewContextChanged(object? sender, EventArgs e) => BindingContext = (sender as BindableObject)?.BindingContext;

    private static void OnSuppressedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var behavior = (HoverBehavior)bindable;
        if ((bool)newValue && behavior._hovering)
        {
            behavior.Apply(false);
        }
    }

    private void Set(bool hovering)
    {
        _hovering = hovering;
        Apply(hovering && !IsSuppressed);
    }

    private void Apply(bool on)
    {
        if (_view is null)
        {
            return;
        }

        if (HoverBackground.Length > 0)
        {
            // Фон в XAML задан значением, а оно сильнее динамического ресурса, поэтому цвет подставляем сами.
            _view.BackgroundColor = on && Application.Current?.Resources.TryGetValue(HoverBackground, out var value) == true && value is Color color
                ? color
                : Colors.Transparent;
        }

        if (HoverStroke.Length > 0 && _view is Border border)
        {
            border.SetDynamicResource(Border.StrokeProperty, on ? HoverStroke : RestStroke);
        }
    }
}
