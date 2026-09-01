namespace TaskManager.Core.Domain;

/// <summary>Тег. Связь с задачами многие-ко-многим.</summary>
public class Tag
{
    public Tag(string name)
    {
        Name = name;
    }

    // Нужен EF Core для создания объекта при чтении из БД.
    private Tag()
    {
        Name = string.Empty;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public ICollection<TaskItem> Tasks { get; } = new List<TaskItem>();
}
