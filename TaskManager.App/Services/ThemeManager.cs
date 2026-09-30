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
/// Switching the Signal theme. The colours are in two dictionaries with the same keys (DarkTheme and LightTheme);
/// on a theme change the dictionary is replaced, and styles that use DynamicResource are recoloured by themselves.
/// </summary>
public static class ThemeManager
{
    public static ThemeMode Current { get; private set; } = ThemeMode.Dark;

    private static readonly WeakEventManager Events = new();

    /// <summary>
    /// The theme changed. Needed by whatever colours itself in code and does not update by itself: the subscription is weak, so on screens
    /// that have closed it does not hold memory. The handler must be an instance method, not a lambda.
    /// </summary>
    public static event EventHandler? Changed
    {
        add => Events.AddEventHandler(value);
        remove => Events.RemoveEventHandler(value);
    }

    /// <summary>Applies the choice from the settings. "System" follows the Windows theme and changes together with it.</summary>
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

    /// <summary>Light themes: the ordinary light one, Solarized Light and Aurora Light. The others are dark.</summary>
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

        // The order of the dictionaries does not matter: styles take colours through DynamicResource and will find them anywhere.
        ResourceDictionary theme = mode switch
        {
            ThemeMode.Dark => new DarkTheme(),
            ThemeMode.Light => new LightTheme(),
            _ => new PaletteTheme(ThemeCatalog.Palette(mode)),
        };
        merged.Add(theme);

        // The Aurora glow (page background, shadow of accent elements) is built from the colours of the theme just chosen.
        merged.Add(new GlowTheme(theme, IsLight(mode)));
        GlowBrushes.Apply(app.Resources, theme, IsLight(mode));

        // Standard elements (menus, dialogs, scroll bars) take the system theme until we say otherwise.
        ApplyToWindow(mode, keepUserTheme);

        if (!keepUserTheme)
        {
            app.UserAppTheme = IsLight(mode) ? AppTheme.Light : AppTheme.Dark;
        }

        Current = mode;
        Events.HandleEvent(null!, EventArgs.Empty, nameof(Changed));
    }

    /// <summary>A window created after the theme was chosen gets it too (drop-down lists take the theme from the window root).</summary>
    public static void AttachWindow(Window window)
    {
        _window = window;
        ApplyToWindow(Current, keepUserTheme: _followSystem);
    }

    private static Window? _window;
    private static bool _followSystem;

    // Drop-down lists and menus are drawn by Windows: without this they take the system theme and may be light in a dark application.
    // The theme is given to the window root and to each such element: a list opens from its own element, not from the root.
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

    /// <summary>The current theme of the application as a WinUI theme, for the windows that we draw ourselves.</summary>
    public static Microsoft.UI.Xaml.ElementTheme NativeTheme => ElementThemeNow();
#endif

    private static bool IsTheme(ResourceDictionary dictionary)
    {
        return dictionary is DarkTheme or LightTheme or PaletteTheme or GlowTheme;
    }
}
