using TaskManager.Core.Services;

namespace TaskManager.Core.Abstractions;

/// <summary>
/// Showing reminders to the user. Core only calculates that it is time to remind, and how to show it (banner,
/// Windows notification, log) is decided by the implementation that the application plugs in.
/// </summary>
public interface IReminderNotifier
{
    /// <summary>
    /// Shows a batch of reminders. If the method throws, the reminders are not considered shown
    /// and will be offered again at the next check.
    /// </summary>
    Task NotifyAsync(IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default);
}
