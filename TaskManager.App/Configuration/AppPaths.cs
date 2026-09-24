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

    // Куда указывает перенос базы. Лежит в папке данных, потому что искать его надо до того, как известен путь к базе.
    private static string LocationFile => Path.Combine(DataDirectory, "database-location.txt");

    /// <summary>
    /// Папка с файлом базы: по умолчанию папка данных, после переноса из настроек та, что записана в указателе.
    /// Если записанной папки больше нет (диск отключён), берётся папка данных.
    /// </summary>
    public static string DatabaseDirectory { get; } = ReadDatabaseDirectory();

    /// <summary>Запоминает новую папку базы; действует после перезапуска приложения.</summary>
    public static void SaveDatabaseDirectory(string directory)
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(LocationFile, directory);
    }

    private static string ReadDatabaseDirectory()
    {
        try
        {
            if (File.Exists(LocationFile))
            {
                var saved = File.ReadAllText(LocationFile).Trim();
                if (saved.Length > 0 && Directory.Exists(saved))
                {
                    return saved;
                }
            }
        }
#pragma warning disable CA1031 // Нечитаемый указатель не должен мешать запуску: берём папку данных.
        catch (Exception)
#pragma warning restore CA1031
        {
        }

        return DataDirectory;
    }
}
