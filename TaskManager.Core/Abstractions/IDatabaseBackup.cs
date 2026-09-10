namespace TaskManager.Core.Abstractions;

/// <summary>Сведения о файле резервной копии, показываемые перед восстановлением.</summary>
/// <param name="TaskCount">Сколько задач (не удалённых) в копии.</param>
/// <param name="MigrationCount">Сколько миграций схемы применено в копии.</param>
public sealed record BackupInfo(int TaskCount, int MigrationCount);

/// <summary>Резервная копия и восстановление базы данных.</summary>
public interface IDatabaseBackup
{
    /// <summary>
    /// Сохраняет согласованную копию текущей базы в указанный файл (существующий файл заменяется).
    /// Базу можно копировать во время работы приложения.
    /// </summary>
    Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет, что файл это целая база TaskManager, которую эта версия программы сможет открыть.
    /// </summary>
    /// <exception cref="TaskManager.Core.Domain.DomainValidationException">Файл не подходит (сообщение объясняет почему).</exception>
    Task<BackupInfo> InspectAsync(string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Готовит восстановление: кладёт копию рядом с базой. Подмена выполнится при следующем запуске приложения
    /// (<see cref="ApplyPendingRestoreAsync"/>), когда база ещё не открыта.
    /// </summary>
    Task StageRestoreAsync(string backupPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Применяет подготовленное восстановление, если оно есть. Вызывать при запуске, до первого обращения к базе.
    /// </summary>
    /// <returns>Сообщение для пользователя о том, что произошло, или null, если восстановления не было.</returns>
    Task<string?> ApplyPendingRestoreAsync(CancellationToken cancellationToken = default);
}
