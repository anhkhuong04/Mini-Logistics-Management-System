using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.CashOnDelivery.GetCodSettlementCandidates;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.CashOnDelivery.MarkCodSettled;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Shops.Notifications;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shops.Reports;
using MiniLogistics.Application.Shipments.AssignShipperToShipment;
using MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Infrastructure.Identity;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class InfrastructurePersistenceTests : IClassFixture<LocalDbIntegrationFixture>
{
    private static readonly Guid DemoAdminUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid DemoOperatorUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid DemoShopUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DemoShipperUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly LocalDbIntegrationFixture _fixture;

    public InfrastructurePersistenceTests(LocalDbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigrateAndSeed_CreatesDemoDataAndSeedIsIdempotent()
    {
        var initialCounts = await GetSeedCountsAsync();

        await _fixture.ExecuteAsync(async services =>
        {
            var seeder = services.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync();
        });

        var repeatedCounts = await GetSeedCountsAsync();

        Assert.Equal(5, initialCounts.Roles);
        Assert.Equal(4, initialCounts.Users);
        Assert.Equal(1, initialCounts.Shops);
        Assert.Equal(1, initialCounts.ApiClients);
        Assert.True(initialCounts.Hubs >= 34);
        Assert.Equal(1, initialCounts.ShipperWorkingAreas);
        Assert.True(initialCounts.FeeRules >= 3);
        Assert.Equal(initialCounts, repeatedCounts);

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var shop = await dbContext.Shops.SingleAsync(item => item.OwnerUserId == DemoShopUserId);
            var apiClient = await dbContext.ApiClients.SingleAsync();

            Assert.Equal(shop.Id, apiClient.ShopId);
            Assert.True(apiClient.IsActive);
            Assert.Equal("Demo E-commerce Integration", apiClient.Name);
            Assert.Equal(ApiKeyHasher.GetPrefix(_fixture.DemoPartnerApiKey), apiClient.ApiKeyPrefix);
            Assert.Equal(ApiKeyHasher.Hash(_fixture.DemoPartnerApiKey), apiClient.ApiKeyHash);
            Assert.NotEqual(_fixture.DemoPartnerApiKey, apiClient.ApiKeyHash);
        });
    }

    [Fact]
    public async Task AutoAssignment_ConcurrentReservationsDoNotExceedShipperCapacityOrDuplicateSideEffects()
    {
        var previousCapacity = await _fixture.ExecuteAsync(async services =>
        {
            var identityService = services.GetRequiredService<IIdentityService>();
            var shipper = (await identityService.GetActiveShippersAsync())
                .Single(item => item.UserId == DemoShipperUserId);
            var counts = await services.GetRequiredService<IShipmentReadRepository>()
                .GetActiveAssignmentCountsByShipperIdsAsync([DemoShipperUserId]);
            counts.TryGetValue(DemoShipperUserId, out var activeLoad);
            return (IsAvailable: shipper.IsAvailableForAssignment,
                Maximum: shipper.MaxActiveShipments,
                ActiveLoad: activeLoad);
        });

        try
        {
            await _fixture.ExecuteAsync(async services =>
            {
                var identityService = services.GetRequiredService<IIdentityService>();
                var availabilityResult = await identityService.SetShipperCapacityAsync(
                    DemoShipperUserId,
                    isAvailableForAssignment: false,
                    maxActiveShipments: previousCapacity.Maximum);
                Assert.True(availabilityResult.IsSuccess, availabilityResult.Error.Description);
            });

            var firstShipment = await CreateShipmentAsync("Concurrent reservation A", codAmount: 0m);
            var secondShipment = await CreateShipmentAsync("Concurrent reservation B", codAmount: 0m);
            Assert.True(firstShipment.IsSuccess, firstShipment.Error.Description);
            Assert.True(secondShipment.IsSuccess, secondShipment.Error.Description);
            Assert.Equal(ShipmentStatus.PendingPickup, firstShipment.Value.Status);
            Assert.Equal(ShipmentStatus.PendingPickup, secondShipment.Value.Status);

            await _fixture.ExecuteAsync(async services =>
            {
                var identityService = services.GetRequiredService<IIdentityService>();
                var availabilityResult = await identityService.SetShipperCapacityAsync(
                    DemoShipperUserId,
                    isAvailableForAssignment: true,
                    maxActiveShipments: previousCapacity.ActiveLoad + 1);
                Assert.True(availabilityResult.IsSuccess, availabilityResult.Error.Description);
            });

            var shipmentIds = new[] { firstShipment.Value.ShipmentId, secondShipment.Value.ShipmentId };
            var outboxCountBefore = await _fixture.ExecuteAsync(services =>
                services.GetRequiredService<MiniLogisticsDbContext>().OutboxMessages
                    .CountAsync(message => shipmentIds.Contains(message.AggregateId)));
            var selectionBarrier = new AssignmentSelectionBarrier(2);

            var assignments = await Task.WhenAll(
                RunAutoAssignmentAsync(shipmentIds[0], selectionBarrier),
                RunAutoAssignmentAsync(shipmentIds[1], selectionBarrier));

            Assert.Equal(1, assignments.Count(result =>
                result.IsSuccess && result.Value.Status == AutoAssignShipmentStatus.Assigned));
            Assert.Equal(1, assignments.Count(result =>
                result.IsSuccess && result.Value.Status == AutoAssignShipmentStatus.NoEligibleShipper));

            await _fixture.ExecuteAsync(async services =>
            {
                var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
                var shipments = await dbContext.Shipments
                    .Include(shipment => shipment.Assignments)
                    .Include(shipment => shipment.StatusHistory)
                    .Where(shipment => shipmentIds.Contains(shipment.Id))
                    .ToListAsync();

                Assert.Equal(2, shipments.Count);
                Assert.Equal(1, shipments.Count(shipment => shipment.Status == ShipmentStatus.Assigned));
                Assert.Equal(1, shipments.Count(shipment => shipment.Status == ShipmentStatus.PendingPickup));
                Assert.Single(
                    shipments.SelectMany(shipment => shipment.Assignments),
                    assignment => assignment.IsActive && assignment.ShipperId == DemoShipperUserId);
                Assert.Equal(1, shipments.Count(shipment => shipment.StatusHistory.Count == 2));
                Assert.Equal(1, shipments.Count(shipment => shipment.StatusHistory.Count == 1));

                var outboxCountAfter = await dbContext.OutboxMessages
                    .CountAsync(message => shipmentIds.Contains(message.AggregateId));
                Assert.Equal(outboxCountBefore + 1, outboxCountAfter);
            });
        }
        finally
        {
            await _fixture.ExecuteAsync(async services =>
            {
                var counts = await services.GetRequiredService<IShipmentReadRepository>()
                    .GetActiveAssignmentCountsByShipperIdsAsync([DemoShipperUserId]);
                counts.TryGetValue(DemoShipperUserId, out var activeLoad);
                var restoreMaximum = Math.Max(previousCapacity.Maximum, activeLoad);
                var identityService = services.GetRequiredService<IIdentityService>();
                var restoreResult = await identityService.SetShipperCapacityAsync(
                    DemoShipperUserId,
                    previousCapacity.IsAvailable,
                    restoreMaximum);
                Assert.True(restoreResult.IsSuccess, restoreResult.Error.Description);
            });
        }
    }

    private Task<MiniLogistics.Domain.Common.Result<AutoAssignShipmentResult>> RunAutoAssignmentAsync(
        Guid shipmentId,
        AssignmentSelectionBarrier selectionBarrier)
    {
        return _fixture.ExecuteAsync(async services =>
        {
            var service = new AutoAssignShipmentService(
                services.GetRequiredService<IShipmentRepository>(),
                new FirstSelectionBarrierSelector(
                    services.GetRequiredService<IShipmentAssignmentSelector>(),
                    selectionBarrier),
                services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IShipperAssignmentCapacityGuard>(),
                services.GetRequiredService<IWebhookEventPublisher>(),
                services.GetRequiredService<IAdminAuditService>(),
                shopNotificationService: services.GetRequiredService<ShopNotificationService>());
            return await service.AutoAssignAsync(shipmentId);
        });
    }

    [Fact]
    public async Task IdentityService_ListUsersWithRoles_ReturnsSeededUsersAndRoles()
    {
        var users = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IIdentityService>().ListUsersWithRolesAsync());

        Assert.Contains(users, user =>
            user.UserId == DemoAdminUserId
            && user.Roles.SequenceEqual(["Admin"]));
        Assert.Contains(users, user =>
            user.UserId == DemoOperatorUserId
            && user.Roles.SequenceEqual(["Operator"]));
        Assert.Contains(users, user =>
            user.UserId == DemoShopUserId
            && user.Roles.SequenceEqual(["Shop"]));
        Assert.Contains(users, user =>
            user.UserId == DemoShipperUserId
            && user.Roles.SequenceEqual(["Shipper"]));
    }

    [Fact]
    public async Task PartnerDashboard_RecentWebhookDeliveries_ReturnsBoundedResultsWithoutSqlTranslationFailure()
    {
        var apiClientId = Guid.Empty;
        var expectedDeliveryIds = new List<Guid>();

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var shopId = await dbContext.Shops
                .Where(shop => shop.OwnerUserId == DemoShopUserId)
                .Select(shop => shop.Id)
                .SingleAsync();
            var now = DateTimeOffset.UtcNow;
            var apiKey = $"ml_test_dashboard_{Guid.NewGuid():N}";
            var apiClient = new ApiClient(
                shopId,
                "Dashboard query regression client",
                ApiKeyHasher.GetPrefix(apiKey),
                ApiKeyHasher.Hash(apiKey),
                now);
            var endpoint = new WebhookEndpoint(
                apiClient.Id,
                "https://partner.example/webhooks/logistics",
                "protected-test-secret",
                now);

            dbContext.ApiClients.Add(apiClient);
            dbContext.WebhookEndpoints.Add(endpoint);
            for (var index = 0; index < 12; index++)
            {
                var delivery = new WebhookDelivery(
                    Guid.NewGuid(),
                    endpoint.Id,
                    apiClient.Id,
                    "shipment.status_changed",
                    Guid.NewGuid(),
                    "{}",
                    now.AddMinutes(index));
                dbContext.WebhookDeliveries.Add(delivery);
                expectedDeliveryIds.Add(delivery.Id);
            }

            await dbContext.SaveChangesAsync();
            apiClientId = apiClient.Id;
        });

        var recent = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IWebhookDeliveryRepository>()
                .GetRecentByApiClientIdsAsync([apiClientId], takePerClient: 10));

        Assert.Equal(10, recent.Count);
        Assert.All(recent, delivery => Assert.Equal(apiClientId, delivery.ApiClientId));
        Assert.Equal(
            expectedDeliveryIds.Skip(2).Reverse(),
            recent.Select(delivery => delivery.Id));
    }

    [Fact]
    public async Task PartnerDashboard_BatchedHistoryQueries_ReturnTopTenForEveryClient()
    {
        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var shopId = await dbContext.Shops
                .Where(shop => shop.OwnerUserId == DemoShopUserId)
                .Select(shop => shop.Id)
                .SingleAsync();
            var now = DateTimeOffset.UtcNow;
            var apiClients = Enumerable.Range(0, 2)
                .Select(index =>
                {
                    var apiKey = $"ml_test_batched_dashboard_{index}_{Guid.NewGuid():N}";
                    return new ApiClient(
                        shopId,
                        $"Batched dashboard client {index}",
                        ApiKeyHasher.GetPrefix(apiKey),
                        ApiKeyHasher.Hash(apiKey),
                        now);
                })
                .ToArray();

            foreach (var apiClient in apiClients)
            {
                var endpoint = new WebhookEndpoint(
                    apiClient.Id,
                    $"https://{apiClient.Id:N}.partner.example/webhooks",
                    "protected-test-secret",
                    now);
                dbContext.ApiClients.Add(apiClient);
                dbContext.WebhookEndpoints.Add(endpoint);

                for (var index = 0; index < 12; index++)
                {
                    var createdAtUtc = now.AddMinutes(-index);
                    dbContext.WebhookDeliveries.Add(new WebhookDelivery(
                        Guid.NewGuid(),
                        endpoint.Id,
                        apiClient.Id,
                        "shipment.status_changed",
                        Guid.NewGuid(),
                        "{}",
                        createdAtUtc));
                    dbContext.PartnerApiCredentialAudits.Add(new PartnerApiCredentialAudit(
                        DemoShopUserId,
                        shopId,
                        apiClient.Id,
                        "ApiKey.Rotated",
                        isSuccess: true,
                        createdAtUtc));
                    dbContext.PartnerApiRequestAudits.Add(new PartnerApiRequestAudit(
                        apiClient.Id,
                        shopId,
                        "POST",
                        "/api/v1/partner/shipments",
                        $"trace-{apiClient.Id:N}-{index}",
                        $"order-{apiClient.Id:N}-{index}",
                        $"idem-{apiClient.Id:N}-{index}",
                        new string('A', 64),
                        StatusCodes.Status400BadRequest,
                        durationMs: index + 1,
                        isSuccess: false,
                        isIdempotentReplay: false,
                        shipmentId: null,
                        trackingCode: null,
                        errorCode: "Application.ValidationFailed",
                        errorMessage: "Invalid request.",
                        createdAtUtc: createdAtUtc));
                }

                dbContext.PartnerApiRequestAudits.Add(new PartnerApiRequestAudit(
                    apiClient.Id,
                    shopId,
                    "GET",
                    "/api/v1/partner/shipments/MLTEST",
                    $"trace-success-{apiClient.Id:N}",
                    externalOrderId: null,
                    idempotencyKey: null,
                    requestHash: new string('B', 64),
                    statusCode: StatusCodes.Status200OK,
                    durationMs: 5,
                    isSuccess: true,
                    isIdempotentReplay: false,
                    shipmentId: null,
                    trackingCode: "MLTEST",
                    errorCode: null,
                    errorMessage: null,
                    createdAtUtc: now));
            }

            await dbContext.SaveChangesAsync();
            var apiClientIds = apiClients.Select(client => client.Id).ToArray();
            var deliveries = await services.GetRequiredService<IWebhookDeliveryRepository>()
                .GetRecentByApiClientIdsAsync(apiClientIds, takePerClient: 10);
            var credentialAudits = await services.GetRequiredService<IPartnerApiCredentialAuditRepository>()
                .GetRecentByApiClientIdsAsync(apiClientIds, takePerClient: 10);
            var usage = await services.GetRequiredService<IPartnerApiRequestAuditRepository>()
                .GetUsageByApiClientIdsAsync(apiClientIds, now);

            Assert.Equal(20, deliveries.Count);
            Assert.Equal(20, credentialAudits.Count);
            foreach (var apiClientId in apiClientIds)
            {
                Assert.Equal(10, deliveries.Count(delivery => delivery.ApiClientId == apiClientId));
                Assert.Equal(10, credentialAudits.Count(audit => audit.ApiClientId == apiClientId));
                Assert.Equal(13, usage[apiClientId].TotalRequests);
                Assert.Equal(1, usage[apiClientId].SuccessfulRequests);
                Assert.Equal(12, usage[apiClientId].FailedRequests);
                Assert.Equal(10, usage[apiClientId].LatestFailedRequests.Count);
            }

            await dbContext.WebhookDeliveries
                .Where(delivery => apiClientIds.Contains(delivery.ApiClientId))
                .ExecuteDeleteAsync();
            await dbContext.PartnerApiRequestAudits
                .Where(audit => apiClientIds.Contains(audit.ApiClientId))
                .ExecuteDeleteAsync();
            await dbContext.PartnerApiCredentialAudits
                .Where(audit => audit.ApiClientId.HasValue && apiClientIds.Contains(audit.ApiClientId.Value))
                .ExecuteDeleteAsync();
            await dbContext.WebhookEndpoints
                .Where(endpoint => apiClientIds.Contains(endpoint.ApiClientId))
                .ExecuteDeleteAsync();
            await dbContext.ApiClients
                .Where(client => apiClientIds.Contains(client.Id))
                .ExecuteDeleteAsync();
        });
    }

    [Fact]
    public async Task PartnerTracking_AfterOperationalStatusUpdate_ReturnsCurrentStatusAndTimeline()
    {
        var createResult = await CreateShipmentAsync("Partner tracking status sync", codAmount: 125_000m);
        Assert.True(createResult.IsSuccess, createResult.Error.Description);
        Assert.Equal(ShipmentStatus.Assigned, createResult.Value.Status);

        var updateResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IUpdateShipmentStatusService>().UpdateAsync(
                new UpdateShipmentStatusCommand(
                    createResult.Value.ShipmentId,
                    DemoShipperUserId,
                    ShipmentStatus.PickingUp,
                    "Shipper started pickup.")));
        Assert.True(updateResult.IsSuccess, updateResult.Error.Description);

        var trackingResult = await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var apiClient = await dbContext.ApiClients.AsNoTracking().SingleAsync(client =>
                client.Name == "Demo E-commerce Integration");
            return await services.GetRequiredService<IPartnerShipmentQueryService>().GetAsync(
                new PartnerGetShipmentCommand(
                    apiClient.Id,
                    apiClient.ShopId,
                    createResult.Value.TrackingCode));
        });

        Assert.True(trackingResult.IsSuccess, trackingResult.Error.Description);
        Assert.Equal(ShipmentStatus.PickingUp, trackingResult.Value.Status);
        Assert.Equal(ShipmentStatus.PickingUp, trackingResult.Value.Timeline.Last().Status);
        Assert.Contains(
            trackingResult.Value.Timeline,
            item => item.Status == ShipmentStatus.Assigned);
    }

    [Fact]
    public async Task CreateShipment_PersistsFeeBreakdownCodAndStatusHistory()
    {
        var createResult = await CreateShipmentAsync("Integration Persist", codAmount: 150_000m);

        Assert.True(createResult.IsSuccess, createResult.Error.Description);
        Assert.Equal(ShipmentStatus.Assigned, createResult.Value.Status);

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var shipment = await dbContext.Shipments
                .Include(item => item.Assignments)
                .Include(item => item.StatusHistory)
                .SingleAsync(item => item.Id == createResult.Value.ShipmentId);
            var codTransaction = await dbContext.CodTransactions
                .SingleAsync(item => item.ShipmentId == createResult.Value.ShipmentId);

            Assert.Equal(createResult.Value.TrackingCode, shipment.TrackingCode.Value);
            Assert.Equal(RouteType.InterRegion, shipment.RouteType);
            Assert.Equal(createResult.Value.ChargeableWeightKg, shipment.ChargeableWeight.Kilograms);
            Assert.Equal(createResult.Value.BaseFeeAmount, shipment.ShippingFeeBreakdown.BaseFee.Amount);
            Assert.Equal(createResult.Value.ExtraWeightFeeAmount, shipment.ShippingFeeBreakdown.ExtraWeightFee.Amount);
            Assert.Equal(createResult.Value.InsuranceFeeAmount, shipment.ShippingFeeBreakdown.InsuranceFee.Amount);
            Assert.Equal(createResult.Value.ReturnFeeAmount, shipment.ShippingFeeBreakdown.ReturnFee.Amount);
            Assert.Equal(createResult.Value.ShippingFeeAmount, shipment.ShippingFee.Amount);
            Assert.Contains(shipment.Assignments, assignment =>
                assignment.IsActive && assignment.ShipperId == DemoShipperUserId);
            Assert.Equal(CodStatus.PendingCollection, codTransaction.Status);
            Assert.Equal(150_000m, codTransaction.Amount.Amount);
            Assert.Contains(shipment.StatusHistory, history =>
                history.Status == ShipmentStatus.PendingPickup
                && history.ChangedByUserId == DemoShopUserId);
            Assert.Contains(shipment.StatusHistory, history =>
                history.Status == ShipmentStatus.Assigned
                && history.ChangedByUserId == SystemActorIds.AutoAssignment);
        });

        var pendingResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IGetPendingPickupShipmentsService>().GetAsync());

        Assert.True(pendingResult.IsSuccess, pendingResult.Error.Description);
        Assert.DoesNotContain(pendingResult.Value, shipment => shipment.ShipmentId == createResult.Value.ShipmentId);
    }

    [Fact]
    public async Task ShopDashboardKpi_ReadsConvertedMoneyColumns()
    {
        var createResult = await CreateShipmentAsync("Integration Dashboard", codAmount: 175_000m);
        Assert.True(createResult.IsSuccess, createResult.Error.Description);

        var dashboardResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IGetShopDashboardKpiService>().GetAsync(
                new ShopDashboardKpiQuery(DemoShopUserId)));

        Assert.True(dashboardResult.IsSuccess, dashboardResult.Error.Description);
        Assert.True(dashboardResult.Value.TotalShipments > 0);
        Assert.True(dashboardResult.Value.TotalShippingFee >= createResult.Value.ShippingFeeAmount);
        Assert.True(dashboardResult.Value.PendingCodAmount >= 175_000m);
    }

    [Fact]
    public async Task DeliveryCodFlow_PersistsAssignmentsStatusCodAndWorkspaceQueries()
    {
        var createResult = await CreateShipmentAsync("Integration COD", codAmount: 250_000m);
        Assert.True(createResult.IsSuccess, createResult.Error.Description);
        Assert.Equal(ShipmentStatus.Assigned, createResult.Value.Status);
        var shipmentId = createResult.Value.ShipmentId;

        var operationsAfterAssign = await GetOperationsAsync();
        Assert.Contains(operationsAfterAssign, shipment =>
            shipment.ShipmentId == shipmentId
            && shipment.Status == ShipmentStatus.Assigned
            && shipment.ActiveShipperId == DemoShipperUserId);

        var shipperAfterAssign = await GetShipperWorkspaceAsync();
        Assert.Contains(shipperAfterAssign, shipment => shipment.ShipmentId == shipmentId);

        foreach (var status in new[]
                 {
                     ShipmentStatus.PickingUp,
                     ShipmentStatus.PickedUp,
                     ShipmentStatus.InTransit,
                     ShipmentStatus.Delivering,
                     ShipmentStatus.Delivered
                 })
        {
            var updateResult = await _fixture.ExecuteAsync(services =>
                services.GetRequiredService<IUpdateShipmentStatusService>().UpdateAsync(new UpdateShipmentStatusCommand(
                    shipmentId,
                    DemoShipperUserId,
                    status,
                    $"Integration move to {status}.")));

            Assert.True(updateResult.IsSuccess, updateResult.Error.Description);
        }

        var deliveredOperations = await GetOperationsAsync();
        Assert.Contains(deliveredOperations, shipment =>
            shipment.ShipmentId == shipmentId
            && shipment.Status == ShipmentStatus.Delivered
            && shipment.CodStatus == CodStatus.PendingCollection);

        var deliveredShipperWorkspace = await GetShipperWorkspaceAsync();
        Assert.Contains(deliveredShipperWorkspace, shipment =>
            shipment.ShipmentId == shipmentId
            && shipment.Status == ShipmentStatus.Delivered
            && shipment.CodStatus == CodStatus.PendingCollection);

        var collectResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IMarkCodCollectedService>().MarkCollectedAsync(new MarkCodCollectedCommand(
                shipmentId,
                DemoShipperUserId)));
        Assert.True(collectResult.IsSuccess, collectResult.Error.Description);

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var shipment = await dbContext.Shipments
                .Include(item => item.Assignments)
                .SingleAsync(item => item.Id == shipmentId);
            var codTransaction = await dbContext.CodTransactions.SingleAsync(item => item.ShipmentId == shipmentId);

            Assert.DoesNotContain(shipment.Assignments, assignment => assignment.IsActive);
            Assert.Equal(CodStatus.Collected, codTransaction.Status);
            Assert.Equal(DemoShipperUserId, codTransaction.CollectedByUserId);
            Assert.NotNull(codTransaction.CollectedAtUtc);
        });

        var operationsAfterCollected = await GetOperationsAsync();
        Assert.DoesNotContain(operationsAfterCollected, shipment => shipment.ShipmentId == shipmentId);

        var shipperAfterCollected = await GetShipperWorkspaceAsync();
        Assert.DoesNotContain(shipperAfterCollected, shipment => shipment.ShipmentId == shipmentId);

        var settlementCandidates = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IGetCodSettlementCandidatesService>().GetAsync());
        Assert.True(settlementCandidates.IsSuccess, settlementCandidates.Error.Description);
        Assert.Contains(settlementCandidates.Value, candidate =>
            candidate.ShipmentId == shipmentId
            && candidate.CollectedByUserId == DemoShipperUserId);

        var settleResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IMarkCodSettledService>().MarkSettledAsync(new MarkCodSettledCommand(
                shipmentId,
                DemoAdminUserId)));
        Assert.True(settleResult.IsSuccess, settleResult.Error.Description);

        var settlementCandidatesAfterSettle = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IGetCodSettlementCandidatesService>().GetAsync());
        Assert.True(settlementCandidatesAfterSettle.IsSuccess, settlementCandidatesAfterSettle.Error.Description);
        Assert.DoesNotContain(settlementCandidatesAfterSettle.Value, candidate => candidate.ShipmentId == shipmentId);
    }

    [Fact]
    public async Task CancelAndReturnedShipments_CloseAssignmentsAndAreHiddenFromWorkspaces()
    {
        var cancelledShipment = await CreateAndAssignShipmentAsync("Integration Cancel", codAmount: 100_000m);

        var cancelResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<ICancelShipmentForCurrentShopService>().CancelAsync(new CancelShipmentCommand(
                DemoShopUserId,
                cancelledShipment.ShipmentId,
                "Integration cancel.")));
        Assert.True(cancelResult.IsSuccess, cancelResult.Error.Description);

        await AssertShipmentHasNoActiveAssignmentAsync(cancelledShipment.ShipmentId, ShipmentStatus.Cancelled);
        Assert.DoesNotContain(await GetOperationsAsync(), shipment => shipment.ShipmentId == cancelledShipment.ShipmentId);
        Assert.DoesNotContain(await GetShipperWorkspaceAsync(), shipment => shipment.ShipmentId == cancelledShipment.ShipmentId);

        var returnedShipment = await CreateAndAssignShipmentAsync("Integration Return", codAmount: 100_000m);
        foreach (var status in new[]
                 {
                     ShipmentStatus.PickingUp,
                     ShipmentStatus.PickedUp,
                     ShipmentStatus.Returned
                 })
        {
            var updateResult = await _fixture.ExecuteAsync(services =>
                services.GetRequiredService<IUpdateShipmentStatusService>().UpdateAsync(new UpdateShipmentStatusCommand(
                    returnedShipment.ShipmentId,
                    DemoShipperUserId,
                    status,
                    $"Integration move to {status}.")));

            Assert.True(updateResult.IsSuccess, updateResult.Error.Description);
        }

        await AssertShipmentHasNoActiveAssignmentAsync(returnedShipment.ShipmentId, ShipmentStatus.Returned);
        Assert.DoesNotContain(await GetOperationsAsync(), shipment => shipment.ShipmentId == returnedShipment.ShipmentId);
        Assert.DoesNotContain(await GetShipperWorkspaceAsync(), shipment => shipment.ShipmentId == returnedShipment.ShipmentId);
    }

    private async Task<CreateShipmentResponse> CreateAndAssignShipmentAsync(string receiverName, decimal codAmount)
    {
        var createResult = await CreateShipmentAsync(receiverName, codAmount);
        Assert.True(createResult.IsSuccess, createResult.Error.Description);
        Assert.Equal(ShipmentStatus.Assigned, createResult.Value.Status);
        return createResult.Value;
    }

    private Task<MiniLogistics.Domain.Common.Result<CreateShipmentResponse>> CreateShipmentAsync(
        string receiverName,
        decimal codAmount)
    {
        return _fixture.ExecuteAsync(services =>
            services.GetRequiredService<ICreateShipmentService>().CreateAsync(new CreateShipmentCommand(
                DemoShopUserId,
                "Demo Mini Shop",
                "0900000001",
                receiverName,
                "0911111111",
                new ShipmentAddressDto("123 Nguyen Trai", "Phuong Ben Thanh", "Ho Chi Minh"),
                new ShipmentAddressDto("9 Pho Hue", "Phuong Trang Tien", "Ha Noi"),
                WeightKg: 1.2m,
                LengthCm: 30m,
                WidthCm: 20m,
                HeightCm: 15m,
                GoodsValueAmount: 3_000_000m,
                CodAmount: codAmount,
                Note: "Integration test shipment.")));
    }

    private async Task<IReadOnlyList<GetOperationsShipmentResponse>> GetOperationsAsync()
    {
        var result = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IGetOperationsShipmentsService>().GetAsync());

        Assert.True(result.IsSuccess, result.Error.Description);
        return result.Value;
    }

    private async Task<IReadOnlyList<GetAssignedShipmentForShipperResponse>> GetShipperWorkspaceAsync()
    {
        var result = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IGetAssignedShipmentsForShipperService>().GetAsync(DemoShipperUserId));

        Assert.True(result.IsSuccess, result.Error.Description);
        return result.Value;
    }

    private async Task AssertShipmentHasNoActiveAssignmentAsync(Guid shipmentId, ShipmentStatus expectedStatus)
    {
        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var shipment = await dbContext.Shipments
                .Include(item => item.Assignments)
                .SingleAsync(item => item.Id == shipmentId);

            Assert.Equal(expectedStatus, shipment.Status);
            Assert.DoesNotContain(shipment.Assignments, assignment => assignment.IsActive);
        });
    }

    private Task<SeedCounts> GetSeedCountsAsync()
    {
        return _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            return new SeedCounts(
                await dbContext.Roles.CountAsync(),
                await dbContext.Users.CountAsync(),
                await dbContext.Shops.CountAsync(),
                await dbContext.ApiClients.CountAsync(),
                await dbContext.Hubs.CountAsync(),
                await dbContext.ShipperWorkingAreas.CountAsync(),
                await dbContext.FeeRules.CountAsync());
        });
    }

    private sealed class AssignmentSelectionBarrier(int participantCount)
    {
        private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivedCount;

        public async Task WaitForAllAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _arrivedCount) == participantCount)
            {
                _released.TrySetResult();
            }

            await _released.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    private sealed class FirstSelectionBarrierSelector(
        IShipmentAssignmentSelector inner,
        AssignmentSelectionBarrier barrier) : IShipmentAssignmentSelector
    {
        private int _hasWaited;

        public async Task<ShipmentAssignmentSelectionResult> SelectAsync(
            Shipment shipment,
            CancellationToken cancellationToken = default)
        {
            var selection = await inner.SelectAsync(shipment, cancellationToken);
            if (Interlocked.Exchange(ref _hasWaited, 1) == 0)
            {
                await barrier.WaitForAllAsync(cancellationToken);
            }

            return selection;
        }
    }

    private sealed record SeedCounts(
        int Roles,
        int Users,
        int Shops,
        int ApiClients,
        int Hubs,
        int ShipperWorkingAreas,
        int FeeRules);
}
