using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.CashOnDelivery;

/// <summary>
/// Provides domain helpers or errors for Cod Errors.
/// </summary>
public static class CodErrors
{
    public static readonly Error CollectionNotRequired = new(
        "COD.CollectionNotRequired",
        "COD collection is not required for this shipment.");

    public static readonly Error ShipmentMustBeDelivered = new(
        "COD.ShipmentMustBeDelivered",
        "COD can only be collected for delivered shipments.");

    public static readonly Error CannotCollect = new(
        "COD.CannotCollect",
        "Only pending COD can be collected.");

    public static readonly Error CannotSettle = new(
        "COD.CannotSettle",
        "Only collected COD can be settled.");

    public static readonly Error CannotChangeAmount = new(
        "COD.CannotChangeAmount",
        "COD amount can only be changed before collection starts.");

    public static readonly Error CollectedAmountCurrencyMismatch = new(
        "COD.CollectedAmountCurrencyMismatch",
        "Collected COD amount must use the declared COD currency.");

    public static readonly Error DiscrepancyNoteRequired = new(
        "COD.DiscrepancyNoteRequired",
        "COD collection note is required when actual collected amount differs from declared amount.");
}
