namespace TaskManager.Core.Domain;

/// <summary>Прогресс по подзадачам: сколько выполнено из скольких ("3/5").</summary>
public readonly record struct TaskProgress(int Completed, int Total)
{
    /// <summary>Подзадач нет вообще.</summary>
    public bool HasSubtasks => Total > 0;

    /// <summary>Все учитываемые подзадачи выполнены (или их нет), родителя можно закрывать.</summary>
    public bool IsComplete => Completed == Total;

    /// <summary>Процент выполнения, 0-100. Без подзадач возвращает 0.</summary>
    public int Percent => Total == 0 ? 0 : Completed * 100 / Total;

    public override string ToString() => $"{Completed}/{Total}";
}
