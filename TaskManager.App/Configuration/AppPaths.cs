namespace TaskManager.App.Configuration;

/// <summary>Where the application keeps its files.</summary>
public static class AppPaths
{
    /// <summary>
    /// Data folder: %LOCALAPPDATA%\TaskManager. Not the program folder: it may be read-only,
    /// and here the user easily finds the database and the logs for a backup.
    /// </summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TaskManager");

    public static string LogsDirectory { get; } = Path.Combine(DataDirectory, "logs");

    // Where the database move points to. It lives in the data folder, because it has to be found before the database path is known.
    private static string LocationFile => Path.Combine(DataDirectory, "database-location.txt");

    /// <summary>
    /// The folder with the database file: the data folder by default, after a move from the settings the one written in the pointer.
    /// If the recorded folder no longer exists (a disk is disconnected), the data folder is used.
    /// </summary>
    public static string DatabaseDirectory { get; } = ReadDatabaseDirectory();

    /// <summary>Remembers the new database folder; takes effect after the application restarts.</summary>
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
#pragma warning disable CA1031 // An unreadable pointer must not prevent start-up: we use the data folder.
        catch (Exception)
#pragma warning restore CA1031
        {
        }

        return DataDirectory;
    }
}
