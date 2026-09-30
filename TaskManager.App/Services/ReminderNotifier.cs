using TaskManager.App.ViewModels;
using TaskManager.Core.Abstractions;
using TaskManager.Core.Services;

namespace TaskManager.App.Services;

/// <summary>
/// Showing reminders to the user: a banner in the side panel. It also writes them to the log, as before.
/// Registered before <c>AddInfrastructure</c>, so it replaces the log writer that is used by default.
/// </summary>
public sealed class ReminderNotifier(ReminderBannerViewModel banner, UserSettings settings, ILogger<ReminderNotifier> logger)
    : IReminderNotifier
{
    public async Task NotifyAsync(IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reminders);

        foreach (var reminder in reminders)
        {
            logger.LogInformation(
                "Напоминание: задача #{TaskId} «{Title}», вид {Kind}, дедлайн {Deadline:u}",
                reminder.TaskId,
                reminder.Title,
                reminder.Kind,
                reminder.Deadline);
        }

        // If showing failed, the exception returns to the reminder service: the reminders are not marked as shown
        // and will be offered again at the next check.
        await banner.ShowAsync(reminders);

#if WINDOWS
        // A system notification: the nearest reminder and how many more are left. Switched on in the settings.
        if (settings.ShowWindowsToast && reminders.Count > 0)
        {
            var first = reminders.OrderBy(r => r.Deadline).First();
            Platforms.Windows.SystemToast.Show(
                first.Title,
                first.Deadline.ToLocalTime().ToString("dd.MM HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                reminders.Count > 1 ? Loc.F("… и ещё {0}", reminders.Count - 1) : string.Empty);
        }
#endif
    }
}
