using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.CashOnDelivery.MarkCodSettled;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Operations;

public interface IOperationsActionService
{
    Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(Guid actor, Guid shipmentId, CancellationToken token);
    Task<Result> StatusAsync(UpdateShipmentStatusCommand command, CancellationToken token);
    Task<Result> CollectAsync(MarkCodCollectedCommand command, CancellationToken token);
    Task<Result> SettleAsync(MarkCodSettledCommand command, CancellationToken token);
}

// Shared status/COD services also serve Shipper; this Operations entry point must require operational permission.
public sealed class OperationsActionService(IOperationAuthorizationService authorization,
    IUpdateShipmentStatusService status, IMarkCodCollectedService collect, IMarkCodSettledService settle,
    IAutoAssignShipmentService autoAssign) : IOperationsActionService
{
    private Task<Result> Authorize(Guid actor, string permission, CancellationToken token) => authorization.EnsurePermissionAsync(
        actor, permission, "User not found.", "User inactive.", "Permission required.", token);
    public async Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(Guid actor, Guid shipmentId, CancellationToken token)
    {
        var access = await Authorize(actor, OperationPermissions.AssignmentRetryAuto, token);
        return access.IsFailure ? Result<AutoAssignShipmentResult>.Failure(access.Error)
            : await autoAssign.AutoAssignAsync(shipmentId, token, actor);
    }
    public async Task<Result> StatusAsync(UpdateShipmentStatusCommand command, CancellationToken token)
    {
        var access = await Authorize(command.ChangedByUserId, OperationPermissions.ShipmentStatusUpdate, token);
        return access.IsFailure ? access : await status.UpdateAsync(command, token);
    }
    public async Task<Result> CollectAsync(MarkCodCollectedCommand command, CancellationToken token)
    {
        var access = await Authorize(command.CollectedByUserId, OperationPermissions.CodCollect, token);
        return access.IsFailure ? access : await collect.MarkCollectedAsync(command, token);
    }
    public async Task<Result> SettleAsync(MarkCodSettledCommand command, CancellationToken token)
    {
        var access = await Authorize(command.SettledByUserId, OperationPermissions.CodSettle, token);
        return access.IsFailure ? access : await settle.MarkSettledAsync(command, token);
    }
}
