namespace MiniLogistics.Application.PartnerApi;

public enum PartnerReferenceUniqueConstraint
{
    IdempotencyKey = 1,
    ExternalOrderId = 2
}

public sealed class PartnerReferenceUniqueConstraintException : Exception
{
    public PartnerReferenceUniqueConstraintException(
        PartnerReferenceUniqueConstraint constraint,
        Exception innerException)
        : base("A partner shipment reference unique constraint was violated.", innerException)
    {
        Constraint = constraint;
    }

    public PartnerReferenceUniqueConstraint Constraint { get; }
}
