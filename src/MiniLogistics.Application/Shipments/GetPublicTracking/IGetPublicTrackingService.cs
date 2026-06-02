using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetPublicTracking;

public interface IGetPublicTrackingService
{
    Task<Result<PublicTrackingResponse>> GetAsync(
        string trackingCode,
        CancellationToken cancellationToken = default);
}
