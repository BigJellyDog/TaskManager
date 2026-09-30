namespace TaskManager.App.Services;

/// <summary>One theme for the menu: the choice in the settings, the name and three colours for the sample (background, surface, accent).</summary>
public sealed record ThemeInfo(ThemeChoice Choice, string Name, string Bg, string Surface, string Accent);

/// <summary>
/// The list of themes as in the Signal mockup: two main ones (dark and light) and five additional ones. "System" follows Windows
/// and comes first. The colours of the additional themes are here too and are turned into the resource dictionary <see cref="PaletteTheme"/>.
/// </summary>
public static class ThemeCatalog
{
    public static IReadOnlyList<ThemeInfo> All { get; } =
    [
        new(ThemeChoice.System, "Системная", "#090B0F", "#12161C", "#C6F25A"),
        new(ThemeChoice.AuroraDark, "Аврора", "#06060C", "#11111F", "#A78BFA"),
        new(ThemeChoice.AuroraLight, "Аврора светлая", "#F4F2FF", "#FFFFFF", "#6D3FE0"),
        new(ThemeChoice.Dark, "Тёмная", "#090B0F", "#12161C", "#C6F25A"),
        new(ThemeChoice.Light, "Светлая", "#F1F3F6", "#FFFFFF", "#476F00"),
        new(ThemeChoice.Dracula, "Dracula", "#282A36", "#2B2D3A", "#BD93F9"),
        new(ThemeChoice.Nord, "Nord", "#262B35", "#2A2F3A", "#88C0D0"),
        new(ThemeChoice.Gruvbox, "Gruvbox", "#282828", "#2D2B2A", "#FABD2F"),
        new(ThemeChoice.Tokyo, "Токийская ночь", "#1A1B26", "#24283B", "#7AA2F7"),
        new(ThemeChoice.Solar, "Solarized светлая", "#FDF6E3", "#FFFBEE", "#1A6498"),
    ];

    public static ThemeInfo Of(ThemeChoice choice) => All.First(t => t.Choice == choice);

    /// <summary>The name in the interface language (for Dracula, Nord and others it is the same in all languages).</summary>
    public static string DisplayName(ThemeInfo theme) => Loc.T(theme.Name);

    /// <summary>A sample of the theme for the menu; for "System" the sample of the theme that is currently on is taken.</summary>
    public static string[] Swatch(ThemeChoice choice)
    {
        var info = choice == ThemeChoice.System
            ? Of(Enum.Parse<ThemeChoice>(ThemeManager.Current.ToString()))
            : Of(choice);
        return [info.Bg, info.Surface, info.Accent];
    }

    // Status and priority colours: the dark themes share them, Solarized Light has its own (as the "Light" one).
    private static readonly Dictionary<string, string> DarkStatus = new()
    {
        ["PCrit"] = "#FF7B7B", ["PHigh"] = "#F5A04A", ["PMed"] = "#6FB4FF", ["PLow"] = "#9BA7B6",
        ["SToDo"] = "#9BA7B6", ["SProg"] = "#6FB4FF", ["SBlock"] = "#FF8F86", ["SDone"] = "#5FD38A", ["SCancel"] = "#A3A3AB",
    };

    private static readonly Dictionary<string, string> LightStatus = new()
    {
        ["PCrit"] = "#B31F28", ["PHigh"] = "#964700", ["PMed"] = "#0B57A6", ["PLow"] = "#4C5662",
        ["SToDo"] = "#4C5662", ["SProg"] = "#0B57A6", ["SBlock"] = "#B31F28", ["SDone"] = "#11662D", ["SCancel"] = "#5B5B63",
    };

    /// <summary>The colours of an additional theme under the same keys as in DarkTheme.xaml and LightTheme.xaml.</summary>
    public static Dictionary<string, string> Palette(ThemeMode mode)
    {
        // Order: Bg, Side, Surface, SurfaceRaised, Line, Ink, InkMuted, Accent, OnAccent, AccentSoft.
        var v = mode switch
        {
            ThemeMode.Dracula => new[] { "#282A36", "#1F2029", "#2B2D3A", "#2D3040", "#44475A", "#F8F8F2", "#B9BDD3", "#BD93F9", "#1E1033", "#3A3558" },
            ThemeMode.Nord => new[] { "#262B35", "#21252E", "#2A2F3A", "#2D3340", "#4C566A", "#ECEFF4", "#C0C8D8", "#88C0D0", "#1B2A30", "#36495A" },
            ThemeMode.Gruvbox => new[] { "#282828", "#1D2021", "#2D2B2A", "#2F2D2C", "#504945", "#EBDBB2", "#BDAE93", "#FABD2F", "#2A1F00", "#4A3F1A" },
            ThemeMode.Tokyo => new[] { "#1A1B26", "#16161E", "#24283B", "#292E42", "#3B4261", "#C0CAF5", "#9AA5CE", "#7AA2F7", "#0F1A33", "#24325A" },
            ThemeMode.AuroraDark => new[] { "#06060C", "#0B0B16", "#11111F", "#191930", "#2A2A47", "#F1F0FF", "#AAA8CA", "#A78BFA", "#140A33", "#2A2152" },
            ThemeMode.AuroraLight => new[] { "#F4F2FF", "#FBFAFF", "#FFFFFF", "#EFECFF", "#D8D2F4", "#170F33", "#4F4778", "#6D3FE0", "#FFFFFF", "#ECE6FF" },
            _ => new[] { "#FDF6E3", "#F1EBD8", "#FFFBEE", "#F7F1DE", "#DDD6C1", "#073642", "#4A5F67", "#1A6498", "#FFFFFF", "#E1EDF5" },
        };

        var keys = new[] { "Bg", "Side", "Surface", "SurfaceRaised", "Line", "Ink", "InkMuted", "Accent", "OnAccent", "AccentSoft" };
        var palette = new Dictionary<string, string>();
        for (var i = 0; i < keys.Length; i++)
        {
            palette[keys[i]] = v[i];
        }

        // Board column: the raised surface at 55% over the background, as in the mockup.
        palette["Column"] = Mix(v[3], v[0], 0.55);

        // The background of the "overdue" tag: the overdue colour at 10% over the surface.
        var light = ThemeManager.IsLight(mode);
        palette["PCritBg"] = Mix(light ? LightStatus["PCrit"] : DarkStatus["PCrit"], v[2], 0.1);

        foreach (var pair in light ? LightStatus : DarkStatus)
        {
            palette[pair.Key] = pair.Value;
        }

        // The second colour of the gradient (button, bars, glow): the partner of the accent in every theme.
        palette["A2"] = mode switch
        {
            ThemeMode.Dracula => "#8BE9FD",
            ThemeMode.Nord => "#B48EAD",
            ThemeMode.Gruvbox => "#FE8019",
            ThemeMode.Tokyo => "#BB9AF7",
            ThemeMode.Solar => "#14766F",
            ThemeMode.AuroraDark => "#22D3EE",
            _ => "#0A7C94",
        };

        return palette;
    }

    internal static string Mix(string top, string bottom, double share)
    {
        var a = Color.FromArgb(top);
        var b = Color.FromArgb(bottom);
        int Channel(float x, float y) => (int)Math.Round((x * 255 * share) + (y * 255 * (1 - share)));
        return $"#{Channel(a.Red, b.Red):X2}{Channel(a.Green, b.Green):X2}{Channel(a.Blue, b.Blue):X2}";
    }
}

/// <summary>A theme built from a colour table: colours and brushes under the same keys as in DarkTheme and LightTheme.</summary>
public sealed class PaletteTheme : ResourceDictionary
{
    public PaletteTheme(IReadOnlyDictionary<string, string> colors)
    {
        foreach (var (key, hex) in colors)
        {
            var color = Color.FromArgb(hex);
            this[key] = color;
            if (key != "Column")
            {
                this[key + "Brush"] = new SolidColorBrush(color);
            }
        }
    }
}

/// <summary>
/// Brushes of the Aurora design: the page background, the gradient of cards, of the primary button and of the selected menu item. Each brush is created
/// once and lives in the application resources, and on a theme change it is only recoloured in place. Replacing the brush itself in the
/// theme dictionary does not work: elements with a style bound to a DynamicResource brush sometimes stayed in the colours of the previous theme.
/// </summary>
public static class GlowBrushes
{
    private static readonly RadialGradientBrush PageGlow = new(
        [new GradientStop(Colors.Black, 0f), new GradientStop(Colors.Black, 1f)], new Point(0, 0), 1.1f);

    private static readonly LinearGradientBrush CardGradient = new(
        [new GradientStop(Colors.Black, 0f), new GradientStop(Colors.Black, 1f)], new Point(0, 0), new Point(0, 1));

    private static readonly LinearGradientBrush AccentSoftGradient = new(
        [new GradientStop(Colors.Black, 0f), new GradientStop(Colors.Black, 1f)], new Point(0, 0), new Point(1, 0));

    private static readonly LinearGradientBrush AccentGradient = new(
        [new GradientStop(Colors.Black, 0f), new GradientStop(Colors.Black, 1f)], new Point(0, 0), new Point(1, 1));

    private static readonly SolidColorBrush AccentBorder = new(Colors.Black);

    /// <summary>Puts the brushes into the application resources (once) and recolours them to the colours of the theme.</summary>
    public static void Apply(ResourceDictionary appResources, ResourceDictionary theme, bool light)
    {
        ArgumentNullException.ThrowIfNull(appResources);
        ArgumentNullException.ThrowIfNull(theme);

        appResources["PageGlow"] = PageGlow;
        appResources["CardGradient"] = CardGradient;
        appResources["AccentSoftGradient"] = AccentSoftGradient;
        appResources["AccentGradient"] = AccentGradient;
        appResources["AccentBorder"] = AccentBorder;

        Color Get(string key, Color fallback) => theme.TryGetValue(key, out var v) && v is Color c ? c : fallback;
        var bg = Get("Bg", Colors.Black);
        var accent = Get("Accent", Colors.Gray);
        var second = Get("A2", accent);
        var surface = Get("Surface", bg);
        var raised = Get("SurfaceRaised", surface);

        // Page background: a soft spot of the accent in the top left corner.
        PageGlow.GradientStops[0].Color = Color.FromArgb(ThemeCatalog.Mix(accent.ToArgbHex(), bg.ToArgbHex(), light ? 0.12 : 0.20));
        PageGlow.GradientStops[1].Color = bg;

        // Cards and panels: slightly lighter at the top (the raised surface at 60%), the ordinary surface at the bottom.
        CardGradient.GradientStops[0].Color = Color.FromArgb(ThemeCatalog.Mix(raised.ToArgbHex(), surface.ToArgbHex(), 0.6));
        CardGradient.GradientStops[1].Color = surface;

        // The selected menu item: a gradient from the accent (22%) to the second colour (8%) and an accent border (45%).
        AccentSoftGradient.GradientStops[0].Color = accent.WithAlpha(0.22f);
        AccentSoftGradient.GradientStops[1].Color = second.WithAlpha(0.08f);
        AccentBorder.Color = accent.WithAlpha(0.45f);

        // The primary button and bars: from the accent to the second colour of the theme.
        AccentGradient.GradientStops[0].Color = accent;
        AccentGradient.GradientStops[1].Color = second;
    }
}

/// <summary>The glow colours and the second accent of the theme (the shadow of accent elements, the colour A2). Plain colours and brushes are swapped with the dictionary.</summary>
public sealed class GlowTheme : ResourceDictionary
{
    public GlowTheme(ResourceDictionary theme, bool light)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var accent = theme.TryGetValue("Accent", out var a) && a is Color ac ? ac : Colors.Gray;
        var second = theme.TryGetValue("A2", out var s) && s is Color sc ? sc : accent;

        this["Glow"] = accent.WithAlpha(light ? 0.18f : 0.30f);
        this["GlowBrush"] = new SolidColorBrush(accent.WithAlpha(light ? 0.18f : 0.30f));
        this["A2Brush"] = new SolidColorBrush(second);
    }
}
