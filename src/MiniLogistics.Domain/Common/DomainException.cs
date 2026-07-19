namespace MiniLogistics.Domain.Common;

/// <summary>
/// Represents Domain Exception in the domain model.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(Error error)
        : base(error.Description)
    {
        Error = error;
    }

    public Error? Error { get; }
}
