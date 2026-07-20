using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.CashOnDelivery.GetShipperCodDailySummary;
using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ShipperCodDailySummaryRepository : IShipperCodDailySummaryRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ShipperCodDailySummaryRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ShipperCodDailySummaryResponse> GetAsync(
        Guid shipperUserId,
        DateTimeOffset dayStartUtc,
        DateTimeOffset dayEndUtc,
        CancellationToken cancellationToken = default)
    {
        var assignedShipmentIds = _dbContext.ShipmentAssignments
            .AsNoTracking()
            .Where(assignment => assignment.ShipperId == shipperUserId)
            .Select(assignment => assignment.ShipmentId)
            .Distinct();

        var codQuery = _dbContext.CodTransactions
            .AsNoTracking()
            .Where(cod => assignedShipmentIds.Contains(cod.ShipmentId));

        var pending = await codQuery
            .Where(cod => cod.Status == CodStatus.PendingCollection)
            .Select(cod => cod.Amount.Amount)
            .ToListAsync(cancellationToken);
        var collectedToday = await codQuery
            .Where(cod => cod.Status == CodStatus.Collected || cod.Status == CodStatus.Settled)
            .Where(cod => cod.CollectedAtUtc >= dayStartUtc && cod.CollectedAtUtc < dayEndUtc)
            .Select(cod => (cod.CollectedAmount ?? cod.Amount).Amount)
            .ToListAsync(cancellationToken);

        return new ShipperCodDailySummaryResponse(
            shipperUserId,
            DateOnly.FromDateTime(dayStartUtc.UtcDateTime),
            pending.Sum(),
            pending.Count,
            collectedToday.Sum(),
            collectedToday.Count,
            "VND");
    }
}
