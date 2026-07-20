using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shipments.ProofOfDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class DeliveryProofRepository : IDeliveryProofRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public DeliveryProofRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DeliveryProof>> GetByShipmentIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeliveryProofs
            .AsNoTracking()
            .Where(proof => proof.ShipmentId == shipmentId)
            .OrderByDescending(proof => proof.SubmittedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(DeliveryProof proof, CancellationToken cancellationToken = default)
    {
        await _dbContext.DeliveryProofs.AddAsync(proof, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
