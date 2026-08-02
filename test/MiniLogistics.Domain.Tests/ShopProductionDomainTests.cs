using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using Xunit;

namespace MiniLogistics.Domain.Tests;

public sealed class ShopProductionDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApiClient_EnforcesScopesIpWhitelistAndExpiration()
    {
        var client = new ApiClient(
            Guid.NewGuid(),
            "Storefront",
            "ml_live_test",
            "hash",
            Now,
            PartnerApiScope.Quote | PartnerApiScope.TrackShipment,
            ["203.0.113.10", "::ffff:203.0.113.11"],
            Now.AddDays(30));

        Assert.True(client.HasScope(PartnerApiScope.Quote));
        Assert.False(client.HasScope(PartnerApiScope.CreateShipment));
        Assert.True(client.IsIpAllowed("203.0.113.10"));
        Assert.True(client.IsIpAllowed("203.0.113.11"));
        Assert.False(client.IsIpAllowed("203.0.113.12"));
        Assert.False(client.IsExpired(Now.AddDays(29)));
        Assert.True(client.IsExpired(Now.AddDays(30)));
    }

    [Fact]
    public void ShipmentImportBatch_TracksPartialFailureAndCompletion()
    {
        var batch = new ShipmentImportBatch(Guid.NewGuid(), Guid.NewGuid(), Now);
        var validRow = new ShipmentImportBatchRow(batch.Id, batch.ShopId, 2, "ORDER-1", "{}", true, Now);
        var invalidRow = new ShipmentImportBatchRow(batch.Id, batch.ShopId, 3, "ORDER-2", "{}", false, Now, "Invalid ward");
        batch.AddRow(validRow);
        batch.AddRow(invalidRow);

        Assert.Equal(ShipmentImportBatchStatus.Pending, batch.Status);
        Assert.Equal(2, batch.TotalRows);
        Assert.Equal(1, batch.ValidRows);
        Assert.Equal(1, batch.FailedRows);

        validRow.MarkCreated(Guid.NewGuid(), "ML202608020001", Now.AddMinutes(1));
        batch.RefreshProgress(Now.AddMinutes(1));

        Assert.Equal(ShipmentImportBatchStatus.CompletedWithErrors, batch.Status);
        Assert.Equal(1, batch.CreatedRows);
        Assert.Equal(1, batch.FailedRows);
        Assert.NotNull(batch.CompletedAtUtc);
    }

    [Fact]
    public void WebhookDelivery_FailedDeliveryCanBeQueuedForRetry()
    {
        var delivery = new WebhookDelivery(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "shipment.updated", Guid.NewGuid(), "{}", Now);
        delivery.MarkFailed(500, "server error", Now, nextAttemptAtUtc: null);

        var result = delivery.Retry(Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(Now.AddMinutes(1), delivery.NextAttemptAtUtc);
        Assert.Null(delivery.LastError);
    }
}
