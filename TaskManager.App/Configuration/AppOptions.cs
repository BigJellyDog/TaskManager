namespace TaskManager.App.Configuration;

/// <summary>Application settings, the "TaskManager" section of appsettings.json.</summary>
public sealed class AppOptions
{
    public const string SectionName = "TaskManager";

    /// <summary>The name of the database file. It lives in the application data folder (<see cref="AppPaths.DataDirectory"/>).</summary>
    public string DatabaseFileName { get; set; } = "taskmanager.db";

    /// <summary>How many tasks to show on one page of the list.</summary>
    public int DefaultPageSize { get; set; } = 20;
}
