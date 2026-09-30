namespace TaskManager.App.Services;

/// <summary>
/// A request from another screen to open the task list with a ready filter ("Show in list" on the statistics tiles).
/// The list takes it once when it is shown.
/// </summary>
public static class ListIntent
{
    public const string Overdue = "overdue";

    public const string Blocked = "blocked";

    private static string? s_pending;

    public static void Request(string intent) => s_pending = intent;

    /// <summary>Returns the request and forgets it at once.</summary>
    public static string? Take()
    {
        var intent = s_pending;
        s_pending = null;
        return intent;
    }
}
