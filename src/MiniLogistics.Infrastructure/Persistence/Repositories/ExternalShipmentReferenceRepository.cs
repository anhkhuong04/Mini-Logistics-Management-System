using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ExternalShipmentReferenceRepository : IExternalShipmentReferenceRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ExternalShipmentReferenceRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ExternalShipmentReference?> GetByApiClientAndIdempotencyKeyAsync(
        Guid apiClientId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalShipmentReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                reference => reference.ApiClientId == apiClientId && reference.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public Task<ExternalShipmentReference?> GetByApiClientAndExternalOrderIdAsync(
        Guid apiClientId,
        string externalOrderId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalShipmentReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                reference => reference.ApiClientId == apiClientId && reference.ExternalOrderId == externalOrderId,
                cancellationToken);
    }

    public Task<ExternalShipmentReference?> GetByApiClientAndShipmentIdAsync(
        Guid apiClientId,
        Guid shipmentId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalShipmentReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                reference => reference.ApiClientId == apiClientId && reference.ShipmentId == shipmentId,
                cancellationToken);
    }

    public async Task AddAsync(
        ExternalShipmentReference reference,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ExternalShipmentReferences.AddAsync(reference, cancellationToken);
    }
}
