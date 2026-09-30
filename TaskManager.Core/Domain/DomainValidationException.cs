namespace TaskManager.Core.Domain;

/// <summary>A business rule of the domain was violated (an empty title, a deadline in the past, etc.). The message is suitable to show to the user.</summary>
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
