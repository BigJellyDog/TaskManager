namespace TaskManager.App.Services;

/// <summary>
/// Просьба другого экрана открыть список задач с готовым фильтром («Показать в списке» на плитках статистики).
/// Список забирает её при показе один раз.
/// </summary>
public static class ListIntent
{
    public const string Overdue = "overdue";

    public const string Blocked = "blocked";

    private static string? _pending;

    public static void Request(string intent) => _pending = intent;

    /// <summary>Возвращает просьбу и сразу забывает её.</summary>
    public static string? Take()
    {
        var intent = _pending;
        _pending = null;
        return intent;
    }
}
