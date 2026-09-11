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

    public static void Apply(ThemeMode mode)
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
        app.UserAppTheme = mode == ThemeMode.Dark ? AppTheme.Dark : AppTheme.Light;
        Current = mode;
    }

    private static bool IsTheme(ResourceDictionary dictionary)
    {
        return dictionary is DarkTheme or LightTheme;
    }
}
