using System.Globalization;
using System.Text;
using TaskManager.Core.Domain;
using TaskStatus = TaskManager.Core.Domain.TaskStatus;

namespace TaskManager.Core.Export;

/// <summary>Turns the export document into Markdown: a checklist with nested subtasks.</summary>
public static class MarkdownRenderer
{
    /// <param name="document">What to output.</param>
    /// <param name="zone">The time zone in which deadlines are shown.</param>
    /// <param name="translate">
    /// Translation of the Russian labels ("Задачи", "в работе", "Высокий" and others) into the user's language. Without it the document is Russian.
    /// </param>
    public static string Render(ExportDocument document, TimeZoneInfo zone, Func<string, string>? translate = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(zone);

        var t = translate ?? (text => text);

        var ids = document.Tasks.Select(t => t.Id).ToHashSet();
        var childrenOf = document.Tasks
            .Where(t => t.ParentId is { } p && ids.Contains(p))
            .ToLookup(t => t.ParentId!.Value);

        // Roots: tasks without a parent and tasks whose parent did not get into the export (for example, deleted).
        var roots = document.Tasks.Where(t => t.ParentId is not { } p || !ids.Contains(p));

        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"# {t("Задачи")}\n\n");
        text.Append(CultureInfo.InvariantCulture, $"{t("Экспорт от")} {TimeZoneInfo.ConvertTime(document.ExportedAt, zone):yyyy-MM-dd HH:mm}\n\n");

        foreach (var root in roots)
        {
            AppendTask(text, root, childrenOf, zone, t, depth: 0);
        }

        return text.ToString();
    }

    private static void AppendTask(
        StringBuilder text,
        ExportTask task,
        ILookup<int, ExportTask> childrenOf,
        TimeZoneInfo zone,
        Func<string, string> t,
        int depth)
    {
        var indent = new string(' ', depth * 2);
        var title = Inline(task.Title);
        var box = task.Status == TaskStatus.Done ? "x" : " ";

        // A cancelled task is struck through: it has no check mark, but it does not look "open" either.
        var shown = task.Status == TaskStatus.Cancelled ? $"~~{title}~~" : title;

        text.Append(CultureInfo.InvariantCulture, $"{indent}- [{box}] {shown}");

        var priority = task.Priority switch
        {
            Priority.Critical => "Критический",
            Priority.High => "Высокий",
            Priority.Medium => "Средний",
            _ => "Низкий",
        };
        var details = new List<string> { t(priority) };

        var state = task.Status switch
        {
            TaskStatus.InProgress => "в работе",
            TaskStatus.Blocked => "заблокирована",
            TaskStatus.Cancelled => "отменена",
            _ => null,
        };
        if (state is not null)
        {
            details.Add(t(state));
        }

        if (task.Deadline is { } deadline)
        {
            details.Add($"{t("до ")}{TimeZoneInfo.ConvertTime(deadline, zone):yyyy-MM-dd HH:mm}");
        }

        if (task.Tags.Count > 0)
        {
            details.Add(string.Join(' ', task.Tags.Select(t => "#" + t.Replace(' ', '_'))));
        }

        text.Append(" (").AppendJoin(", ", details).Append(")\n");

        // The description as a quote under the item: indented two spaces deeper than the item itself.
        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            foreach (var line in task.Description.ReplaceLineEndings("\n").Split('\n'))
            {
                text.Append(CultureInfo.InvariantCulture, $"{indent}  > {line}\n");
            }
        }

        foreach (var child in childrenOf[task.Id])
        {
            AppendTask(text, child, childrenOf, zone, t, depth + 1);
        }
    }

    // The title in Markdown stays on one line.
    private static string Inline(string value) => value.ReplaceLineEndings(" ");
}
