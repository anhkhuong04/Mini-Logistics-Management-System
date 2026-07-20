using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.CashOnDelivery.GetShipperCodDailySummary;

public interface IGetShipperCodDailySummaryService
{
    Task<Result<ShipperCodDailySummaryResponse>> GetAsync(
        Guid shipperUserId,
        DateOnly? businessDate = null,
        CancellationToken cancellationToken = default);
}
