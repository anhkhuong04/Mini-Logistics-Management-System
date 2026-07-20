namespace MiniLogistics.Application.CashOnDelivery.GetShipperCodDailySummary;

public interface IShipperCodDailySummaryRepository
{
    Task<ShipperCodDailySummaryResponse> GetAsync(
        Guid shipperUserId,
        DateTimeOffset dayStartUtc,
        DateTimeOffset dayEndUtc,
        CancellationToken cancellationToken = default);
}
