namespace TaskManager.App.Configuration;

/// <summary>Где приложение хранит свои файлы.</summary>
public static class AppPaths
{
    /// <summary>
    /// Папка данных: %LOCALAPPDATA%\TaskManager. Не папка программы: она может быть недоступна для записи,
    /// а здесь пользователь легко найдёт базу и журналы для резервной копии.
    /// </summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskManager");

    public static string LogsDirectory { get; } = Path.Combine(DataDirectory, "logs");
}
