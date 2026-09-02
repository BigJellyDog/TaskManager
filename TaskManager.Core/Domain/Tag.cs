namespace TaskManager.Core.Domain;

/// <summary>Тег. Связь с задачами многие-ко-многим.</summary>
public class Tag
{
    public const int MaxNameLength = 50;

    public Tag(string name)
    {
        Name = NormalizeName(name);
        NormalizedName = Normalize(Name);
    }

    // Нужен EF Core для создания объекта при чтении из БД.
    private Tag()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Имя в верхнем регистре: по нему ищем дубли. SQLite NOCASE не умеет в кириллицу, поэтому нормализуем сами.</summary>
    public string NormalizedName { get; private set; }

    public ICollection<TaskItem> Tasks { get; } = new List<TaskItem>();

    /// <summary>Переименование тега затрагивает все задачи, потому что они ссылаются на один и тот же объект.</summary>
    public void Rename(string name)
    {
        Name = NormalizeName(name);
        NormalizedName = Normalize(Name);
    }

    /// <summary>Приводит имя к виду, в котором сравниваются теги.</summary>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
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
