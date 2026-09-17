using TaskManager.App.Resources.Styles;

namespace TaskManager.App.Services;

public enum ThemeMode
{
    Dark,
    Light,
}

/// <summary>
/// Переключение темы Signal. Цвета лежат в двух словарях с одинаковыми ключами (DarkTheme и LightTheme);
/// при смене темы подменяется словарь, а стили, использующие DynamicResource, перекрашиваются сами.
/// </summary>
public static class ThemeManager
{
    public static ThemeMode Current { get; private set; } = ThemeMode.Dark;

    /// <summary>Применяет выбор из настроек. «Системная» следует за темой Windows и меняется вместе с ней.</summary>
    public static void Apply(ThemeChoice choice)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        if (choice == ThemeChoice.System)
        {
            app.UserAppTheme = AppTheme.Unspecified;
            Apply(app.RequestedTheme == AppTheme.Light ? ThemeMode.Light : ThemeMode.Dark, keepUserTheme: true);
            return;
        }

        Apply(choice == ThemeChoice.Light ? ThemeMode.Light : ThemeMode.Dark);
    }

    public static void Apply(ThemeMode mode) => Apply(mode, keepUserTheme: false);

    private static void Apply(ThemeMode mode, bool keepUserTheme)
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var merged = app.Resources.MergedDictionaries;
        foreach (var old in merged.Where(IsTheme).ToList())
        {
            merged.Remove(old);
        }

        // Порядок словарей не важен: стили берут цвета через DynamicResource и найдут их в любом месте.
        merged.Add(mode == ThemeMode.Dark ? new DarkTheme() : new LightTheme());

        // Стандартные элементы (меню, диалоги, полосы прокрутки) берут тему системы, пока мы не скажем иначе.
        ApplyToWindow(mode, keepUserTheme);

        if (!keepUserTheme)
        {
            app.UserAppTheme = mode == ThemeMode.Dark ? AppTheme.Dark : AppTheme.Light;
        }

        Current = mode;
    }

    /// <summary>Окно, созданное после выбора темы, тоже получает её (раскрывающиеся списки берут тему от корня окна).</summary>
    public static void AttachWindow(Window window)
    {
        _window = window;
        ApplyToWindow(Current, keepUserTheme: _followSystem);
    }

    private static Window? _window;
    private static bool _followSystem;

    // Раскрывающиеся списки и меню рисует Windows: без этого они берут тему системы и могут быть светлыми в тёмном приложении.
    // Тему получают корень окна и каждый такой элемент: список раскрывается из своего элемента, а не из корня.
    private static void ApplyToWindow(ThemeMode mode, bool keepUserTheme)
    {
        _followSystem = keepUserTheme;
        _mode = mode;
#if WINDOWS
        var theme = ElementThemeNow();
        if ((_window ?? Application.Current?.Windows.FirstOrDefault())?.Handler?.PlatformView is Microsoft.UI.Xaml.Window native
            && native.Content is Microsoft.UI.Xaml.FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }

        _native.RemoveAll(reference => !reference.TryGetTarget(out _));
        foreach (var reference in _native)
        {
            if (reference.TryGetTarget(out var element))
            {
                element.RequestedTheme = theme;
            }
        }
#endif
    }

    private static ThemeMode _mode = ThemeMode.Dark;

#if WINDOWS
    private static readonly List<WeakReference<Microsoft.UI.Xaml.FrameworkElement>> _native = [];

    private static Microsoft.UI.Xaml.ElementTheme ElementThemeNow() =>
        _followSystem
            ? Microsoft.UI.Xaml.ElementTheme.Default
            : _mode == ThemeMode.Dark ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light;

    /// <summary>Текущая тема приложения в виде темы WinUI, для окон, которые мы рисуем сами.</summary>
    public static Microsoft.UI.Xaml.ElementTheme NativeTheme => ElementThemeNow();

    /// <summary>Подключает системный элемент с раскрывающимся окном (список, календарь) к теме приложения.</summary>
    public static void Register(Microsoft.UI.Xaml.FrameworkElement element)
    {
        _native.Add(new WeakReference<Microsoft.UI.Xaml.FrameworkElement>(element));
        element.RequestedTheme = ElementThemeNow();
    }
#endif

    private static bool IsTheme(ResourceDictionary dictionary)
    {
        return dictionary is DarkTheme or LightTheme;
    }
}
