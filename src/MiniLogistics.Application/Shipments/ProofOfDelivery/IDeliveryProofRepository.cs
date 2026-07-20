using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.ProofOfDelivery;

public interface IDeliveryProofRepository
{
    Task<IReadOnlyList<DeliveryProof>> GetByShipmentIdAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(DeliveryProof proof, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
