namespace TaskManager.App.Configuration;

/// <summary>Настройки приложения, секция "TaskManager" в appsettings.json.</summary>
public sealed class AppOptions
{
    public const string SectionName = "TaskManager";

    /// <summary>Имя файла базы данных. Лежит в папке данных приложения (<see cref="AppPaths.DataDirectory"/>).</summary>
    public string DatabaseFileName { get; set; } = "taskmanager.db";

    /// <summary>Сколько задач показывать на одной странице списка.</summary>
    public int DefaultPageSize { get; set; } = 20;
}
