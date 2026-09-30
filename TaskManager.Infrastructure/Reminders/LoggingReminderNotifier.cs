using Microsoft.Extensions.Logging;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;

namespace TaskManager.Infrastructure.Reminders;

/// <summary>
/// The default way to show reminders: a log entry. The application replaces it with its own implementation
/// (banner, Windows notification) registered before the call to <c>AddInfrastructure</c>.
/// </summary>
public class LoggingReminderNotifier : IReminderNotifier
{
    private readonly ILogger<LoggingReminderNotifier> _logger;

    public LoggingReminderNotifier(ILogger<LoggingReminderNotifier> logger)
    {
        _logger = logger;
    }

    public Task NotifyAsync(IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminders);

        foreach (var reminder in reminders)
        {
            var text = reminder.Kind switch
            {
                ReminderKind.Overdue => "просрочена",
                ReminderKind.DueIn1Hour => "до дедлайна меньше часа",
                _ => "до дедлайна меньше суток",
            };

            _logger.LogWarning("Напоминание: задача #{TaskId} «{Title}» {Text} (дедлайн {Deadline:u})",
                reminder.TaskId, reminder.Title, text, reminder.Deadline);
        }

        return Task.CompletedTask;
    }
}
