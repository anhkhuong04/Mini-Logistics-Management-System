namespace MiniLogistics.Domain.Common;

/// <summary>
/// Represents the validated Error value used by the domain model.
/// </summary>
public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
