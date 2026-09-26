namespace TaskManager.App.Services;

/// <summary>Одна тема для меню: выбор в настройках, название и три цвета для образца (фон, поверхность, акцент).</summary>
public sealed record ThemeInfo(ThemeChoice Choice, string Name, string Bg, string Surface, string Accent);

/// <summary>
/// Список тем как в макете Signal: две основные (тёмная и светлая) и пять дополнительных. «Системная» следует за Windows
/// и стоит первой. Цвета дополнительных тем лежат здесь же и превращаются в словарь ресурсов <see cref="PaletteTheme"/>.
/// </summary>
public static class ThemeCatalog
{
    public static IReadOnlyList<ThemeInfo> All { get; } =
    [
        new(ThemeChoice.System, "Системная", "#090B0F", "#12161C", "#C6F25A"),
        new(ThemeChoice.AuroraDark, "Aurora", "#06060C", "#11111F", "#A78BFA"),
        new(ThemeChoice.AuroraLight, "Aurora Light", "#F4F2FF", "#FFFFFF", "#6D3FE0"),
        new(ThemeChoice.Dark, "Тёмная", "#090B0F", "#12161C", "#C6F25A"),
        new(ThemeChoice.Light, "Светлая", "#F1F3F6", "#FFFFFF", "#476F00"),
        new(ThemeChoice.Dracula, "Dracula", "#282A36", "#2B2D3A", "#BD93F9"),
        new(ThemeChoice.Nord, "Nord", "#262B35", "#2A2F3A", "#88C0D0"),
        new(ThemeChoice.Gruvbox, "Gruvbox", "#282828", "#2D2B2A", "#FABD2F"),
        new(ThemeChoice.Tokyo, "Tokyo Night", "#1A1B26", "#24283B", "#7AA2F7"),
        new(ThemeChoice.Solar, "Solarized Light", "#FDF6E3", "#FFFBEE", "#1A6498"),
    ];

    public static ThemeInfo Of(ThemeChoice choice) => All.First(t => t.Choice == choice);

    /// <summary>Название на языке интерфейса (у Dracula, Nord и других оно одинаково на всех языках).</summary>
    public static string DisplayName(ThemeInfo theme) => Loc.T(theme.Name);

    /// <summary>Образец темы для меню; у «Системной» берётся образец темы, которая сейчас включена.</summary>
    public static string[] Swatch(ThemeChoice choice)
    {
        var info = choice == ThemeChoice.System
            ? Of(Enum.Parse<ThemeChoice>(ThemeManager.Current.ToString()))
            : Of(choice);
        return [info.Bg, info.Surface, info.Accent];
    }

    // Статусные и приоритетные цвета: у тёмных тем общие, у Solarized Light свои (как у «Светлой»).
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

    /// <summary>Цвета дополнительной темы под теми же ключами, что в DarkTheme.xaml и LightTheme.xaml.</summary>
    public static Dictionary<string, string> Palette(ThemeMode mode)
    {
        // Порядок: Bg, Side, Surface, SurfaceRaised, Line, Ink, InkMuted, Accent, OnAccent, AccentSoft.
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

        // Колонка доски: приподнятая поверхность на 55% поверх фона, как в макете.
        palette["Column"] = Mix(v[3], v[0], 0.55);

        // Фон метки «просрочено»: цвет просрочки на 10% поверх поверхности.
        var light = ThemeManager.IsLight(mode);
        palette["PCritBg"] = Mix(light ? LightStatus["PCrit"] : DarkStatus["PCrit"], v[2], 0.1);

        foreach (var pair in light ? LightStatus : DarkStatus)
        {
            palette[pair.Key] = pair.Value;
        }

        // Второй цвет градиента (кнопка, полосы, свечение): пара к акценту у каждой темы.
        palette["A2"] = mode switch
        {
            ThemeMode.Dracula => "#8BE9FD",
            ThemeMode.Nord => "#B48EAD",
            ThemeMode.Gruvbox => "#FE8019",
            ThemeMode.Tokyo => "#BB9AF7",
            ThemeMode.Solar => "#2AA198",
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

/// <summary>Тема, собранная из таблицы цветов: цвета и кисти под теми же ключами, что в DarkTheme и LightTheme.</summary>
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
/// Свечение дизайна Aurora, собранное из цветов выбранной темы: фон страницы (мягкое пятно акцента в левом верхнем
/// углу) и цвет тени у акцентных элементов. Лежит отдельным словарём, чтобы подходить к любой теме, а не только к Aurora.
/// </summary>
public sealed class GlowTheme : ResourceDictionary
{
    public GlowTheme(ResourceDictionary theme, bool light)
    {
        ArgumentNullException.ThrowIfNull(theme);

        var bg = theme.TryGetValue("Bg", out var b) && b is Color bc ? bc : Colors.Black;
        var accent = theme.TryGetValue("Accent", out var a) && a is Color ac ? ac : Colors.Gray;
        var second = theme.TryGetValue("A2", out var s) && s is Color sc ? sc : accent;

        this["Glow"] = accent.WithAlpha(light ? 0.18f : 0.30f);
        this["GlowBrush"] = new SolidColorBrush(accent.WithAlpha(light ? 0.18f : 0.30f));
        this["A2Brush"] = new SolidColorBrush(second);

        var centre = Color.FromArgb(ThemeCatalog.Mix(accent.ToArgbHex(), bg.ToArgbHex(), light ? 0.12 : 0.20));
        this["PageGlow"] = new RadialGradientBrush(
            [new GradientStop(centre, 0f), new GradientStop(bg, 1f)],
            new Point(0, 0),
            1.1f);

        // Карточки и панели: сверху чуть светлее (приподнятая поверхность на 60%), книзу обычная поверхность.
        var surface = theme.TryGetValue("Surface", out var sf) && sf is Color sfc ? sfc : bg;
        var raised = theme.TryGetValue("SurfaceRaised", out var rs) && rs is Color rsc ? rsc : surface;
        this["CardGradient"] = new LinearGradientBrush(
            [new GradientStop(Color.FromArgb(ThemeCatalog.Mix(raised.ToArgbHex(), surface.ToArgbHex(), 0.6)), 0f), new GradientStop(surface, 1f)],
            new Point(0, 0),
            new Point(0, 1));

        // Выбранный пункт меню: градиент от акцента (22%) к второму цвету (8%) и рамка акцента (45%).
        this["AccentSoftGradient"] = new LinearGradientBrush(
            [new GradientStop(accent.WithAlpha(0.22f), 0f), new GradientStop(second.WithAlpha(0.08f), 1f)],
            new Point(0, 0),
            new Point(1, 0));
        this["AccentBorder"] = new SolidColorBrush(accent.WithAlpha(0.45f));

        // Градиент основной кнопки и полос: от акцента ко второму цвету темы.
        this["AccentGradient"] = new LinearGradientBrush(
            [new GradientStop(accent, 0f), new GradientStop(second, 1f)],
            new Point(0, 0),
            new Point(1, 1));
    }
}
