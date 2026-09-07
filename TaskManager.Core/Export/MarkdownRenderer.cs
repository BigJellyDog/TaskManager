using System.Globalization;
using System.Text;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Export;

/// <summary>Превращает документ экспорта в Markdown: чек-лист с вложенными подзадачами.</summary>
public static class MarkdownRenderer
{
    /// <param name="document">Что выводить.</param>
    /// <param name="zone">Часовой пояс, в котором показываются дедлайны.</param>
    public static string Render(ExportDocument document, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(zone);

        var ids = document.Tasks.Select(t => t.Id).ToHashSet();
        var childrenOf = document.Tasks
            .Where(t => t.ParentId is { } p && ids.Contains(p))
            .ToLookup(t => t.ParentId!.Value);

        // Корни: задачи без родителя и задачи, чей родитель не попал в экспорт (например, удалён).
        var roots = document.Tasks.Where(t => t.ParentId is not { } p || !ids.Contains(p));

        var text = new StringBuilder();
        text.Append("# Задачи\n\n");
        text.Append(CultureInfo.InvariantCulture, $"Экспорт от {TimeZoneInfo.ConvertTime(document.ExportedAt, zone):yyyy-MM-dd HH:mm}\n\n");

        foreach (var root in roots)
        {
            AppendTask(text, root, childrenOf, zone, depth: 0);
        }

        return text.ToString();
    }

    private static void AppendTask(
        StringBuilder text,
        ExportTask task,
        ILookup<int, ExportTask> childrenOf,
        TimeZoneInfo zone,
        int depth)
    {
        var indent = new string(' ', depth * 2);
        var title = Inline(task.Title);
        var box = task.Status == TaskStatus.Done ? "x" : " ";

        // Отменённую задачу зачёркиваем: галочки у неё нет, но и "открытой" она не выглядит.
        var shown = task.Status == TaskStatus.Cancelled ? $"~~{title}~~" : title;

        text.Append(CultureInfo.InvariantCulture, $"{indent}- [{box}] {shown}");

        var details = new List<string> { task.Priority.ToString() };

        var state = task.Status switch
        {
            TaskStatus.InProgress => "в работе",
            TaskStatus.Blocked => "заблокирована",
            TaskStatus.Cancelled => "отменена",
            _ => null,
        };
        if (state is not null)
        {
            details.Add(state);
        }

        if (task.Deadline is { } deadline)
        {
            details.Add($"до {TimeZoneInfo.ConvertTime(deadline, zone):yyyy-MM-dd HH:mm}");
        }

        if (task.Tags.Count > 0)
        {
            details.Add(string.Join(' ', task.Tags.Select(t => "#" + t.Replace(' ', '_'))));
        }

        text.Append(" (").AppendJoin(", ", details).Append(")\n");

        // Описание цитатой под пунктом: отступ на два пробела глубже самого пункта.
        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            foreach (var line in task.Description.ReplaceLineEndings("\n").Split('\n'))
            {
                text.Append(CultureInfo.InvariantCulture, $"{indent}  > {line}\n");
            }
        }

        foreach (var child in childrenOf[task.Id])
        {
            AppendTask(text, child, childrenOf, zone, depth + 1);
        }
    }

    // Название в Markdown остаётся одной строкой.
    private static string Inline(string value) => value.ReplaceLineEndings(" ");
}
