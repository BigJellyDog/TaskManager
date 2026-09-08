using TaskManager.Core.Abstractions;
using TaskManager.Core.Domain;
using TaskManager.Core.Services;

namespace TaskManager.App.Services;

/// <summary>Превращает исключения в понятный пользователю текст. Технические подробности уходят в журнал.</summary>
public static class ErrorMessages
{
    public static string ToUserMessage(Exception exception) => exception switch
    {
        // Сообщения этих исключений уже написаны для пользователя.
        DomainValidationException or InvalidStatusTransitionException or TaskNotFoundException => exception.Message,

        // Задачу изменили между чтением и сохранением (оптимистичная блокировка): предлагаем обновить и повторить.
        ConcurrencyConflictException => "Задачу уже изменили в другом месте. Обновите данные и повторите действие.",

        _ => "Произошла непредвиденная ошибка. Подробности записаны в журнал.",
    };

    /// <summary>Ошибки, о которых пользователю нужно сказать, но которые не говорят о сбое программы.</summary>
    public static bool IsExpected(Exception exception) => exception is
        DomainValidationException or InvalidStatusTransitionException or TaskNotFoundException or ConcurrencyConflictException;
}
