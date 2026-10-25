using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Application.Shippers.GetActiveShippers;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Operations;

public sealed class OperationsReadService(IOperationAuthorizationService authorization,
    IGetOperationsShipmentsService shipments, IGetPendingPickupShipmentsService pending,
    IGetActiveShippersService shippers, IShipmentReadRepository shipmentRepository,
    IOperationsSettlementReadRepository settlements) : IOperationsReadService
{
    private Task<Result> Authorize(Guid actor, string permission, CancellationToken token) => authorization.EnsurePermissionAsync(
        actor, permission, "User not found.", "User inactive.", "Permission required.", token);

    public async Task<Result<PagedResponse<GetOperationsShipmentResponse>>> ShipmentsAsync(Guid actor, GetOperationsShipmentsQuery query, CancellationToken cancellationToken)
    {
        var access = await Authorize(actor, OperationPermissions.ShipmentView, cancellationToken);
        if (access.IsFailure) return Result<PagedResponse<GetOperationsShipmentResponse>>.Failure(access.Error);
        var result = await shipments.SearchAsync(query, cancellationToken);
        if (result.IsFailure) return result;
        var audit = await Authorize(actor, OperationPermissions.AuditView, cancellationToken);
        // Status history remains operationally visible; actor identity and GPS are audit-only.
        if (audit.IsSuccess) return result;
        return Result<PagedResponse<GetOperationsShipmentResponse>>.Success(result.Value with {
            Items = result.Value.Items.Select(item => item with { TrackingHistory = item.TrackingHistory.Select(history => history with {
                ChangedByUserId = Guid.Empty, ChangedByDisplayName = "", ChangedByEmail = null, ChangedByUserFound = false,
                Latitude = null, Longitude = null, GpsAccuracyMeters = null, GpsCapturedAtUtc = null
            }).ToList() }).ToList()
        });
    }
    public async Task<Result<PagedResponse<GetPendingPickupShipmentResponse>>> PendingAsync(Guid actor, GetPendingPickupShipmentsQuery query, CancellationToken cancellationToken)
    {
        var access = await Authorize(actor, OperationPermissions.ShipmentView, cancellationToken);
        return access.IsFailure ? Result<PagedResponse<GetPendingPickupShipmentResponse>>.Failure(access.Error)
            : await pending.SearchAsync(query, cancellationToken);
    }
    public async Task<Result<IReadOnlyList<OperationsShipperResponse>>> ShippersAsync(Guid actor, CancellationToken cancellationToken)
    {
        var access = await Authorize(actor, OperationPermissions.ShipmentView, cancellationToken);
        if (access.IsFailure) return Result<IReadOnlyList<OperationsShipperResponse>>.Failure(access.Error);
        var result = await shippers.GetAsync(cancellationToken);
        if (result.IsFailure) return Result<IReadOnlyList<OperationsShipperResponse>>.Failure(result.Error);
        var counts = await shipmentRepository.GetActiveAssignmentCountsByShipperIdsAsync(result.Value.Select(item => item.UserId).ToList(), cancellationToken);
        return Result<IReadOnlyList<OperationsShipperResponse>>.Success(result.Value.Select(item => new OperationsShipperResponse(
            item.UserId, item.FullName, item.PhoneNumber, item.IsAvailableForAssignment, item.MaxActiveShipments,
            counts.GetValueOrDefault(item.UserId), item.WorkingAreas)).ToList());
    }
    public async Task<Result<PagedResponse<OperationsCodCandidateResponse>>> SettlementAsync(Guid actor, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var access = await Authorize(actor, OperationPermissions.CodSettle, cancellationToken);
        if (access.IsFailure) return Result<PagedResponse<OperationsCodCandidateResponse>>.Failure(access.Error);
        if (pageNumber < 1 || pageSize is < 1 or > 100 || (long)(pageNumber - 1) * pageSize > int.MaxValue)
            return Result<PagedResponse<OperationsCodCandidateResponse>>.Failure(Common.ApplicationErrors.ValidationFailed("Invalid page."));
        return Result<PagedResponse<OperationsCodCandidateResponse>>.Success(await settlements.SearchAsync(pageNumber, pageSize, cancellationToken));
    }
}
