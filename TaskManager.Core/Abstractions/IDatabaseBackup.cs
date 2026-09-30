namespace TaskManager.Core.Abstractions;

/// <summary>Information about a backup file, shown before the restore.</summary>
/// <param name="TaskCount">How many tasks (not deleted) are in the backup.</param>
/// <param name="MigrationCount">How many schema migrations are applied in the backup.</param>
public sealed record BackupInfo(int TaskCount, int MigrationCount);

/// <summary>Backup and restore of the database.</summary>
public interface IDatabaseBackup
{
    /// <summary>
    /// Saves a consistent copy of the current database to the given file (an existing file is replaced).
    /// The database can be copied while the application is running.
    /// </summary>
    Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks that the file is a complete TaskManager database that this version of the program can open.
    /// </summary>
    /// <exception cref="TaskManager.Core.Domain.DomainValidationException">The file does not fit (the message explains why).</exception>
    Task<BackupInfo> InspectAsync(string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares a restore: puts the copy next to the database. The replacement happens at the next start of the application
    /// (<see cref="ApplyPendingRestoreAsync"/>), when the database is not open yet.
    /// </summary>
    Task StageRestoreAsync(string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a prepared restore, if there is one. Call it at start-up, before the first access to the database.
    /// </summary>
    /// <returns>A message for the user about what happened, or null if there was no restore.</returns>
    Task<string?> ApplyPendingRestoreAsync(CancellationToken cancellationToken = default);
}
