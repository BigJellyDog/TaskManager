namespace TaskManager.Core.Domain;

/// <summary>Когда создаётся следующая копия повторяющейся задачи.</summary>
public enum RecurrenceMode
{
    /// <summary>После выполнения текущей: следующая создаётся в момент закрытия.</summary>
    AfterCompletion = 0,

    /// <summary>По календарю: следующая создаётся по расписанию, даже если предыдущая не закрыта.</summary>
    ByCalendar = 1,
}
