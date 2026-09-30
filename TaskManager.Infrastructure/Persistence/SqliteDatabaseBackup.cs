using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;

namespace TaskManager.Infrastructure.Persistence;

public sealed class SqliteDatabaseBackup : IDatabaseBackup
{
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LockPoll = TimeSpan.FromMilliseconds(250);

    // The migrations known to this version of the program. A copy with an unknown migration was made by a newer version.
    private static readonly HashSet<string> KnownMigrations = typeof(TaskManagerDbContext).Assembly
        .GetTypes()
        .Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id)
        .OfType<string>()
        .ToHashSet();

    private readonly string _databasePath;
    private readonly TimeProvider _clock;
    private readonly ILogger<SqliteDatabaseBackup> _logger;

    public SqliteDatabaseBackup(string databasePath, TimeProvider clock, ILogger<SqliteDatabaseBackup> logger)
    {
        _databasePath = Path.GetFullPath(databasePath);
        _clock = clock;
        _logger = logger;
    }

    /// <summary>The waiting file: the copy is put here until it is swapped in at start-up.</summary>
    private string PendingPath => _databasePath + ".restore";

    public async Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        var destination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        // VACUUM INTO refuses to write to an existing file.
        File.Delete(destination);

        // VACUUM INTO makes a complete consistent copy through ordinary reading, so it works while the application uses the database.
        // A plain file copy could catch half of a write or lose data from the WAL journal.
        await using var connection = new SqliteConnection(ConnectionString(_databasePath, SqliteOpenMode.ReadWrite));
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $path";
        command.Parameters.AddWithValue("$path", destination);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<BackupInfo> InspectAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupPath))
        {
            throw new DomainValidationException("Файл не найден.");
        }

        try
        {
            // Read-only and without a connection pool: the file does not change and is not left busy after the check.
            await using var connection = new SqliteConnection(ConnectionString(backupPath, SqliteOpenMode.ReadOnly));
            await connection.OpenAsync(cancellationToken);

            if (await ScalarAsync<string>(connection, "PRAGMA integrity_check", cancellationToken) != "ok")
            {
                throw new DomainValidationException("Файл повреждён: проверка целостности базы не пройдена.");
            }

            var tables = await ScalarAsync<long>(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Tasks', '__EFMigrationsHistory')",
                cancellationToken);
            if (tables != 2)
            {
                throw new DomainValidationException("Это не резервная копия TaskManager.");
            }

            var migrations = new List<string>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    migrations.Add(reader.GetString(0));
                }
            }

            if (migrations.Any(m => !KnownMigrations.Contains(m)))
            {
                throw new DomainValidationException("Копия создана более новой версией приложения и не может быть открыта этой версией.");
            }

            var tasks = await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM Tasks WHERE IsDeleted = 0", cancellationToken);
            return new BackupInfo((int)tasks, migrations.Count);
        }
        catch (SqliteException ex)
        {
            // "file is not a database" and the like: we do not show the SQLite message to the user, it is technical.
            _logger.LogWarning(ex, "Файл не прошёл проверку как резервная копия: {Path}", backupPath);
            throw new DomainValidationException("Файл не является базой данных SQLite или повреждён.", ex);
        }
    }

    public Task StageRestoreAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        File.Copy(backupPath, PendingPath, overwrite: true);
        _logger.LogInformation("Восстановление из копии подготовлено, оно применится при следующем запуске");
        return Task.CompletedTask;
    }

    public async Task<string?> ApplyPendingRestoreAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(PendingPath))
        {
            return null;
        }

        try
        {
            // The application may have just closed and still hold the database file: we wait until it is released.
            await WaitUntilUnlockedAsync(_databasePath, cancellationToken);

            string? safetyCopy = null;
            if (File.Exists(_databasePath))
            {
                // We flush the WAL journal into the main file, otherwise recent changes would be lost in the safety copy.
                await using (var connection = new SqliteConnection(ConnectionString(_databasePath, SqliteOpenMode.ReadWrite)))
                {
                    await connection.OpenAsync(cancellationToken);
                    await using var command = connection.CreateCommand();
                    command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }

                var stamp = _clock.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                safetyCopy = $"{_databasePath}.before-restore-{stamp}.bak";
                File.Copy(_databasePath, safetyCopy, overwrite: true);
            }

            // The leftovers of the old database journal must not mix with the new one.
            File.Delete(_databasePath + "-wal");
            File.Delete(_databasePath + "-shm");
            File.Move(PendingPath, _databasePath, overwrite: true);

            _logger.LogInformation("База восстановлена из копии. Страховочная копия: {SafetyCopy}", safetyCopy);
            return safetyCopy is null
                ? "Данные восстановлены из резервной копии."
                : $"Данные восстановлены из резервной копии. Прежняя база сохранена: {safetyCopy}";
        }
#pragma warning disable CA1031 // A restore error must not prevent start-up: the application keeps working with the old database.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or TimeoutException)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Не удалось применить восстановление из копии");

            // We put the file aside so as not to try to apply it on every start.
            var failed = PendingPath + ".failed";
            File.Move(PendingPath, failed, overwrite: true);
            return $"Не удалось восстановить данные из копии, приложение работает со старой базой. Файл копии оставлен: {failed}";
        }
    }

    private static string ConnectionString(string path, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString();

    private static async Task<T?> ScalarAsync<T>(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? default : (T)value;
    }

    private static async Task WaitUntilUnlockedAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var deadline = DateTime.UtcNow + LockWait;
        while (true)
        {
            try
            {
                // An exclusive open succeeds only when nobody else uses the file.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return;
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(LockPoll, cancellationToken);
            }
            catch (IOException ex)
            {
                throw new TimeoutException("Файл базы данных остался занят другим процессом.", ex);
            }
        }
    }
}
