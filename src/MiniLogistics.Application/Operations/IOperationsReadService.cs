using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Operations;

public sealed record OperationsShipperResponse(Guid UserId, string FullName, string? PhoneNumber,
    bool IsAvailableForAssignment, int MaxActiveShipments, int ActiveShipmentCount,
    IReadOnlyList<ShipperWorkingAreaResponse> WorkingAreas);
public sealed record OperationsCodCandidateResponse(Guid ShipmentId, string TrackingCode, string ReceiverName,
    decimal CodAmount, string Currency, DateTimeOffset? CollectedAtUtc, Guid? CollectedByUserId);

public interface IOperationsSettlementReadRepository
{
    Task<PagedResponse<OperationsCodCandidateResponse>> SearchAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
}

public interface IOperationsReadService
{
    Task<Result<PagedResponse<GetOperationsShipmentResponse>>> ShipmentsAsync(Guid actor, GetOperationsShipmentsQuery query, CancellationToken cancellationToken);
    Task<Result<PagedResponse<GetPendingPickupShipmentResponse>>> PendingAsync(Guid actor, GetPendingPickupShipmentsQuery query, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<OperationsShipperResponse>>> ShippersAsync(Guid actor, CancellationToken cancellationToken);
    Task<Result<PagedResponse<OperationsCodCandidateResponse>>> SettlementAsync(Guid actor, int pageNumber, int pageSize, CancellationToken cancellationToken);
}
