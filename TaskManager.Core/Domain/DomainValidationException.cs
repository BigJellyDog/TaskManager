namespace TaskManager.Core.Domain;

/// <summary>Нарушено бизнес-правило домена (пустое название, дедлайн в прошлом и т.п.). Сообщение пригодно для показа пользователю.</summary>
public class DomainValidationException : Exception
{
    public DomainValidationException()
    {
    }

    public DomainValidationException(string message)
        : base(message)
    {
    }

    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
