namespace MiniLogistics.Application.CashOnDelivery.MarkCodCollected;

public sealed record MarkCodCollectedCommand(
    Guid ShipmentId,
    Guid CollectedByUserId);
