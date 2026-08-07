using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Infrastructure.Persistence;

namespace MiniLogistics.Infrastructure.PartnerApi;

public sealed class PartnerApiRetentionService
{
    private readonly MiniLogisticsDbContext _dbContext;
    private readonly PartnerApiRetentionOptions _options;

    public PartnerApiRetentionService(
        MiniLogisticsDbContext dbContext,
        IOptions<PartnerApiRetentionOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<int> DeleteExpiredAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var total = 0;
        total += await DeleteInBatchesAsync(
            "OutboxMessages",
            _dbContext.OutboxMessages
                .Where(message => message.Status == OutboxMessageStatus.Succeeded
                    && message.ProcessedAtUtc < nowUtc.AddDays(-_options.SucceededQueueRetentionDays)),
            cancellationToken);
        total += await DeleteInBatchesAsync(
            "WebhookDeliveries",
            _dbContext.WebhookDeliveries
                .Where(delivery => delivery.Status == WebhookDeliveryStatus.Succeeded
                    && delivery.LastAttemptAtUtc < nowUtc.AddDays(-_options.SucceededQueueRetentionDays)),
            cancellationToken);
        total += await DeleteInBatchesAsync(
            "PartnerApiRequestAudits",
            _dbContext.PartnerApiRequestAudits
                .Where(audit => audit.CreatedAtUtc < nowUtc.AddDays(-_options.RequestAuditRetentionDays)),
            cancellationToken);
        total += await DeleteInBatchesAsync(
            "PartnerApiCredentialAudits",
            _dbContext.PartnerApiCredentialAudits
                .Where(audit => audit.CreatedAtUtc < nowUtc.AddDays(-_options.CredentialAuditRetentionDays)),
            cancellationToken);
        return total;
    }

    private async Task<int> DeleteInBatchesAsync<TEntity>(
        string table,
        IQueryable<TEntity> query,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var total = 0;
        while (true)
        {
            var deleted = await query
                .Take(_options.BatchSize)
                .ExecuteDeleteAsync(cancellationToken);
            total += deleted;
            PartnerApiWorkerTelemetry.RecordRetention(table, deleted);
            if (deleted < _options.BatchSize)
            {
                return total;
            }
        }
    }
}
