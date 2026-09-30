using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;

namespace TaskManager.App.Services;

/// <summary>Turns exceptions into text the user can understand. Technical details go to the log.</summary>
public static class ErrorMessages
{
    public static string ToUserMessage(Exception exception) => exception switch
    {
        // The messages of these exceptions are already written for the user.
        DomainValidationException or InvalidStatusTransitionException or TaskNotFoundException => Loc.Message(exception.Message),

        // The task was changed between reading and saving (optimistic locking): we suggest to refresh and repeat.
        ConcurrencyConflictException => Loc.T("Задачу уже изменили в другом месте. Обновите данные и повторите действие."),

        _ => Loc.T("Произошла непредвиденная ошибка. Подробности записаны в журнал."),
    };

    /// <summary>Errors the user needs to be told about, but which do not mean a program failure.</summary>
    public static bool IsExpected(Exception exception) => exception is
        DomainValidationException or InvalidStatusTransitionException or TaskNotFoundException or ConcurrencyConflictException;
}
