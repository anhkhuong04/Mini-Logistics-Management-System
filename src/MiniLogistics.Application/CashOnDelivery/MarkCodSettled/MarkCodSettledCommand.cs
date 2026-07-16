namespace MiniLogistics.Application.CashOnDelivery.MarkCodSettled;

public sealed record MarkCodSettledCommand(
    Guid ShipmentId,
    Guid SettledByUserId,
    string? Note = null);
