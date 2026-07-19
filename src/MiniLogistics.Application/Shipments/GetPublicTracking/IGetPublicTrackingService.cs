using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetPublicTracking;

/// <summary>
/// Defines the application use case contract for Get Public Tracking.
/// </summary>
public interface IGetPublicTrackingService
{
    Task<Result<PublicTrackingResponse>> GetAsync(
        string trackingCode,
        string? phoneLast4 = null,
        CancellationToken cancellationToken = default);
}
