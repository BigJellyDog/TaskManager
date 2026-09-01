namespace TaskManager.Core.Domain;

/// <summary>Приоритет задачи. Числа растут вместе с важностью, поэтому сортировка по значению работает как надо.</summary>
public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}
