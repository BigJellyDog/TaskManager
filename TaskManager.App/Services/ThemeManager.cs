using TaskManager.App.Resources.Styles;

namespace TaskManager.App.Services;

public enum ThemeMode
{
    Dark,
    Light,
    Dracula,
    Nord,
    Gruvbox,
    Tokyo,
    Solar,
    AuroraDark,
    AuroraLight,
}

/// <summary>
/// Переключение темы Signal. Цвета лежат в двух словарях с одинаковыми ключами (DarkTheme и LightTheme);
/// при смене темы подменяется словарь, а стили, использующие DynamicResource, перекрашиваются сами.
/// </summary>
public static class ThemeManager
{
    public static ThemeMode Current { get; private set; } = ThemeMode.Dark;

    private static readonly WeakEventManager Events = new();

    /// <summary>
    /// Тема сменилась. Нужна тому, что красит себя в коде и не обновляется само: подписка слабая, поэтому на экранах,
    /// которые закрылись, она не держит память. Обработчик должен быть методом объекта, а не лямбдой.
    /// </summary>
    public static event EventHandler? Changed
    {
        add => Events.AddEventHandler(value);
        remove => Events.RemoveEventHandler(value);
    }

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

        Apply(Enum.Parse<ThemeMode>(choice.ToString()));
    }

    /// <summary>Светлые темы: обычная светлая, Solarized Light и Aurora Light. Остальные тёмные.</summary>
    public static bool IsLight(ThemeMode mode) => mode is ThemeMode.Light or ThemeMode.Solar or ThemeMode.AuroraLight;

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
        ResourceDictionary theme = mode switch
        {
            ThemeMode.Dark => new DarkTheme(),
            ThemeMode.Light => new LightTheme(),
            _ => new PaletteTheme(ThemeCatalog.Palette(mode)),
        };
        merged.Add(theme);

        // Свечение Aurora (фон страницы, тень акцентных элементов) собирается из цветов только что выбранной темы.
        merged.Add(new GlowTheme(theme, IsLight(mode)));
        GlowBrushes.Apply(app.Resources, theme, IsLight(mode));

        // Стандартные элементы (меню, диалоги, полосы прокрутки) берут тему системы, пока мы не скажем иначе.
        ApplyToWindow(mode, keepUserTheme);

        if (!keepUserTheme)
        {
            app.UserAppTheme = IsLight(mode) ? AppTheme.Light : AppTheme.Dark;
        }

        Current = mode;
        Events.HandleEvent(null!, EventArgs.Empty, nameof(Changed));
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
        Window? first = null;
        if (Application.Current?.Windows != null)
        {
            foreach (var window in Application.Current?.Windows!)
            {
                first = window;
                break;
            }
        }

        if ((_window ?? first)?.Handler?.PlatformView is Microsoft.UI.Xaml.Window
            {
                Content: Microsoft.UI.Xaml.FrameworkElement root
            })
        {
            root.RequestedTheme = theme;
        }
#endif
    }

    private static ThemeMode _mode = ThemeMode.Dark;

#if WINDOWS
    private static Microsoft.UI.Xaml.ElementTheme ElementThemeNow() =>
        _followSystem
            ? Microsoft.UI.Xaml.ElementTheme.Default
            : IsLight(_mode) ? Microsoft.UI.Xaml.ElementTheme.Light : Microsoft.UI.Xaml.ElementTheme.Dark;

    /// <summary>Текущая тема приложения в виде темы WinUI, для окон, которые мы рисуем сами.</summary>
    public static Microsoft.UI.Xaml.ElementTheme NativeTheme => ElementThemeNow();
#endif

    private static bool IsTheme(ResourceDictionary dictionary)
    {
        return dictionary is DarkTheme or LightTheme or PaletteTheme or GlowTheme;
    }
}
