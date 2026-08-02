using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Shops.Audit;
using MiniLogistics.Domain.AdminAuditing;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class ShopAuditLogRepository : IShopAuditLogRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ShopAuditLogRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AdminAuditLog>> QueryForShopsAsync(
        Guid currentUserId,
        IReadOnlyCollection<Guid> shopIds,
        ShopAuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var logs = _dbContext.AdminAuditLogs
            .AsNoTracking()
            .Where(log =>
                (log.TargetType == AdminAuditTargetTypes.Shop && shopIds.Contains(log.TargetId))
                || (log.TargetType == AdminAuditTargetTypes.Shipment
                    && _dbContext.Shipments.Any(shipment => shipment.Id == log.TargetId && shopIds.Contains(shipment.ShopId)))
                || (log.TargetType == AdminAuditTargetTypes.CodTransaction
                    && _dbContext.CodTransactions.Any(cod => cod.Id == log.TargetId
                        && _dbContext.Shipments.Any(shipment => shipment.Id == cod.ShipmentId && shopIds.Contains(shipment.ShopId))))
                || (log.TargetType == AdminAuditTargetTypes.PartnerApiClient
                    && _dbContext.ApiClients.Any(client => client.Id == log.TargetId && shopIds.Contains(client.ShopId)))
                || (log.TargetType == AdminAuditTargetTypes.PartnerWebhookEndpoint
                    && _dbContext.WebhookEndpoints.Any(endpoint => endpoint.Id == log.TargetId
                        && _dbContext.ApiClients.Any(client => client.Id == endpoint.ApiClientId && shopIds.Contains(client.ShopId))))
                || (log.TargetType == AdminAuditTargetTypes.WebhookDelivery
                    && _dbContext.WebhookDeliveries.Any(delivery => delivery.Id == log.TargetId
                        && _dbContext.ApiClients.Any(client => client.Id == delivery.ApiClientId && shopIds.Contains(client.ShopId))))
                || (log.TargetType == AdminAuditTargetTypes.ShopStaffMembership
                    && _dbContext.ShopStaffMemberships.Any(membership =>
                        membership.Id == log.TargetId && shopIds.Contains(membership.ShopId))));

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            logs = logs.Where(log => log.Action == action);
        }

        if (query.FromUtc.HasValue)
        {
            logs = logs.Where(log => log.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            logs = logs.Where(log => log.CreatedAtUtc <= query.ToUtc.Value);
        }

        return await logs
            .OrderByDescending(log => log.CreatedAtUtc)
            .ThenByDescending(log => log.Id)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
    }
}
