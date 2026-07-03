using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments;

internal static class ShipmentStatusHistoryMapper
{
    public static async Task<IReadOnlyList<ShipmentStatusHistoryResponse>> ToResponseAsync(
        IEnumerable<ShipmentStatusHistory> statusHistory,
        IIdentityService identityService,
        CancellationToken cancellationToken)
    {
        var orderedHistory = statusHistory
            .OrderBy(history => history.ChangedAtUtc)
            .ToList();

        var changedByUserIds = orderedHistory
            .Select(history => history.ChangedByUserId)
            .Distinct()
            .ToList();

        var users = await identityService.GetUsersByIdsAsync(changedByUserIds, cancellationToken);
        var userById = users.ToDictionary(user => user.UserId);

        return orderedHistory
            .Select(history =>
            {
                if (history.ChangedByUserId == SystemActorIds.AutoAssignment)
                {
                    return new ShipmentStatusHistoryResponse(
                        history.Status,
                        history.Note,
                        history.ChangedAtUtc,
                        history.ChangedByUserId,
                        "Auto assignment engine",
                        null,
                        false);
                }

                userById.TryGetValue(history.ChangedByUserId, out var user);

                return new ShipmentStatusHistoryResponse(
                    history.Status,
                    history.Note,
                    history.ChangedAtUtc,
                    history.ChangedByUserId,
                    user?.FullName ?? "Người dùng không xác định",
                    user?.Email,
                    user is not null);
            })
            .ToList();
    }
}
