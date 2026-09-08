namespace TaskManager.Core.Export;

/// <summary>Одна запись отчёта об импорте: какая задача из файла и что с ней не так.</summary>
/// <param name="FileTaskId">Id задачи в файле.</param>
/// <param name="Title">Название задачи (для показа пользователю).</param>
/// <param name="Reason">Причина.</param>
public sealed record ImportIssue(int? FileTaskId, string? Title, string Reason);

/// <summary>Итог импорта.</summary>
/// <param name="Added">Сколько задач добавлено.</param>
/// <param name="Skipped">Пропущены намеренно: дубликаты, удалённые, подзадачи пропущенного родителя.</param>
/// <param name="Errors">Не импортированы из-за ошибок в данных.</param>
/// <param name="Warnings">Задача добавлена, но часть связей не создана (например, зависимость создала бы цикл).</param>
public sealed record ImportReport(
    int Added,
    IReadOnlyList<ImportIssue> Skipped,
    IReadOnlyList<ImportIssue> Errors,
    IReadOnlyList<ImportIssue> Warnings)
{
    /// <summary>Есть ли что-то, кроме успешно добавленных задач.</summary>
    public bool HasProblems => Skipped.Count > 0 || Errors.Count > 0 || Warnings.Count > 0;
}
