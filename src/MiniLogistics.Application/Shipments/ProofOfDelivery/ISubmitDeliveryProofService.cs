using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.ProofOfDelivery;

public interface ISubmitDeliveryProofService
{
    Task<Result<DeliveryProofResponse>> SubmitAsync(
        SubmitDeliveryProofCommand command,
        CancellationToken cancellationToken = default);
}
