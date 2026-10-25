using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shippers.Workspace;

public sealed record ShipperSelfProfileResponse(Guid UserId, string FullName, string? PhoneNumber,
    bool IsAvailableForAssignment, int MaxActiveShipments, int ActiveShipmentCount);
public interface IShipperSelfService
{
    Task<Result<ShipperSelfProfileResponse>> ProfileAsync(Guid actor, CancellationToken token);
    Task<Result> StatusAsync(UpdateShipmentStatusCommand command, CancellationToken token);
    Task<Result> CollectAsync(MarkCodCollectedCommand command, CancellationToken token);
}
public sealed class ShipperSelfService(IIdentityService identity, IShipmentReadRepository shipments,
    IUpdateShipmentStatusService status, IMarkCodCollectedService collect) : IShipperSelfService
{
    private async Task<Result> Authorize(Guid actor, CancellationToken token)
    {
        var check = await identity.CheckUserRoleAsync(actor, nameof(UserRole.Shipper), token);
        return check.Exists && check.IsActive && check.IsInRole ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden("Active Shipper role required."));
    }
    public async Task<Result<ShipperSelfProfileResponse>> ProfileAsync(Guid actor, CancellationToken token)
    {
        var access = await Authorize(actor, token);
        if (access.IsFailure) return Result<ShipperSelfProfileResponse>.Failure(access.Error);
        var user = (await identity.GetUsersByIdsAsync([actor], token)).SingleOrDefault(item => item.UserId == actor);
        if (user is null) return Result<ShipperSelfProfileResponse>.Failure(ApplicationErrors.NotFound("Shipper not found."));
        var counts = await shipments.GetActiveAssignmentCountsByShipperIdsAsync([actor], token);
        return Result<ShipperSelfProfileResponse>.Success(new(actor, user.FullName, user.PhoneNumber,
            user.IsAvailableForAssignment, user.MaxActiveShipments, counts.GetValueOrDefault(actor)));
    }
    public async Task<Result> StatusAsync(UpdateShipmentStatusCommand command, CancellationToken token)
    {
        var access = await Authorize(command.ChangedByUserId, token);
        return access.IsFailure ? access : await status.UpdateAsync(command with { RequireActiveShipperAssignment = true }, token);
    }
    public async Task<Result> CollectAsync(MarkCodCollectedCommand command, CancellationToken token)
    {
        var access = await Authorize(command.CollectedByUserId, token);
        return access.IsFailure ? access : await collect.MarkCollectedAsync(command with { RequireActiveShipperAssignment = true }, token);
    }
}
