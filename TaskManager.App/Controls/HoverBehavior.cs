namespace TaskManager.App.Controls;

/// <summary>
/// Highlight of the element under the mouse pointer, as in the Signal mockup: menu items and list rows get the background of the "raised"
/// surface, cards on the board get an accent outline. The colours are set by resource keys, so the highlight follows the theme.
/// If <see cref="IsSuppressed"/> is true (for example the item is already selected or the card is overdue), the highlight is not switched on.
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

    /// <summary>The key of the background colour under the pointer; empty if the background does not change.</summary>
    public string HoverBackground
    {
        get => (string)GetValue(HoverBackgroundProperty);
        set => SetValue(HoverBackgroundProperty, value);
    }

    /// <summary>The key of the outline brush under the pointer (Border only); empty if the outline does not change.</summary>
    public string HoverStroke
    {
        get => (string)GetValue(HoverStrokeProperty);
        set => SetValue(HoverStrokeProperty, value);
    }

    /// <summary>The key of the outline brush at rest.</summary>
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

        // The behaviour is attached later than the element is created and inherits its binding context.
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
            // The background is set as a value in XAML, which is stronger than a dynamic resource, so we apply the colour ourselves.
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
