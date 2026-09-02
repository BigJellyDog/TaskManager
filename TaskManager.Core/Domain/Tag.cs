namespace TaskManager.Core.Domain;

/// <summary>Тег. Связь с задачами многие-ко-многим.</summary>
public class Tag
{
    public const int MaxNameLength = 50;

    public Tag(string name)
    {
        Name = NormalizeName(name);
    }

    // Нужен EF Core для создания объекта при чтении из БД.
    private Tag()
    {
        Name = string.Empty;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public ICollection<TaskItem> Tasks { get; } = new List<TaskItem>();

    /// <summary>Переименование тега затрагивает все задачи, потому что они ссылаются на один и тот же объект.</summary>
    public void Rename(string name)
    {
        Name = NormalizeName(name);
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainValidationException("Название тега не может быть пустым.");
        }

        if (trimmed.Length > MaxNameLength)
        {
            throw new DomainValidationException($"Название тега не может быть длиннее {MaxNameLength} символов.");
        }

        return trimmed;
    }
}
