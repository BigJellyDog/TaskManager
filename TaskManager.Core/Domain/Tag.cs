namespace TaskManager.Core.Domain;

/// <summary>A tag. Many-to-many relation with tasks.</summary>
public class Tag
{
    public const int MaxNameLength = 50;

    public Tag(string name)
    {
        Name = NormalizeName(name);
        NormalizedName = Normalize(Name);
    }

    // Needed by EF Core to create the object when reading from the database.
    private Tag()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>The name in upper case: duplicates are searched by it. SQLite NOCASE cannot handle Cyrillic, so we normalise ourselves.</summary>
    public string NormalizedName { get; private set; }

    public ICollection<TaskItem> Tasks { get; } = new List<TaskItem>();

    /// <summary>Renaming a tag affects all tasks, because they refer to the same object.</summary>
    public void Rename(string name)
    {
        Name = NormalizeName(name);
        NormalizedName = Normalize(Name);
    }

    /// <summary>Brings a name to the form in which tags are compared.</summary>
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
