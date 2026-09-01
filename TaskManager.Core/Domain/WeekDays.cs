namespace TaskManager.Core.Domain;

/// <summary>Дни недели как битовые флаги: удобно хранить набор дней одним числом в БД.</summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
}
