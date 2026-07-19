namespace MiniLogistics.Domain.CashOnDelivery;

/// <summary>
/// Defines the supported Cod Status values in the domain model.
/// </summary>
public enum CodStatus
{
    NotRequired = 0,
    PendingCollection = 1,
    Collected = 2,
    Settled = 3
}
