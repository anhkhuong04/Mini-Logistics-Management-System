namespace MiniLogistics.Domain.Common;

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
