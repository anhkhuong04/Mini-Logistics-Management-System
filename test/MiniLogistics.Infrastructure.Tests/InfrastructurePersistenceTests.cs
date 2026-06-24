using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.CashOnDelivery.GetCodSettlementCandidates;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.CashOnDelivery.MarkCodSettled;
using MiniLogistics.Application.Shipments.AssignShipperToShipment;
using MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;
using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Fees;
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

        Assert.Equal(4, initialCounts.Roles);
        Assert.Equal(4, initialCounts.Users);
        Assert.Equal(1, initialCounts.Shops);
        Assert.True(initialCounts.FeeRules >= 3);
        Assert.Equal(initialCounts, repeatedCounts);
    }

    [Fact]
    public async Task CreateShipment_PersistsFeeBreakdownCodAndStatusHistory()
    {
        var createResult = await CreateShipmentAsync("Integration Persist", codAmount: 150_000m);

        Assert.True(createResult.IsSuccess, createResult.Error.Description);
        Assert.Equal(ShipmentStatus.PendingPickup, createResult.Value.Status);

        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var shipment = await dbContext.Shipments
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
            Assert.Equal(CodStatus.PendingCollection, codTransaction.Status);
            Assert.Equal(150_000m, codTransaction.Amount.Amount);
            Assert.Contains(shipment.StatusHistory, history =>
                history.Status == ShipmentStatus.PendingPickup
                && history.ChangedByUserId == DemoShopUserId);
        });

        var pendingResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IGetPendingPickupShipmentsService>().GetAsync());

        Assert.True(pendingResult.IsSuccess, pendingResult.Error.Description);
        Assert.Contains(pendingResult.Value, shipment => shipment.ShipmentId == createResult.Value.ShipmentId);
    }

    [Fact]
    public async Task DeliveryCodFlow_PersistsAssignmentsStatusCodAndWorkspaceQueries()
    {
        var createResult = await CreateShipmentAsync("Integration COD", codAmount: 250_000m);
        Assert.True(createResult.IsSuccess, createResult.Error.Description);
        var shipmentId = createResult.Value.ShipmentId;

        var assignResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IAssignShipperToShipmentService>().AssignAsync(new AssignShipperCommand(
                shipmentId,
                DemoShipperUserId,
                DemoOperatorUserId,
                "Integration assign.")));
        Assert.True(assignResult.IsSuccess, assignResult.Error.Description);

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

        var assignResult = await _fixture.ExecuteAsync(services =>
            services.GetRequiredService<IAssignShipperToShipmentService>().AssignAsync(new AssignShipperCommand(
                createResult.Value.ShipmentId,
                DemoShipperUserId,
                DemoOperatorUserId,
                "Integration assign.")));

        Assert.True(assignResult.IsSuccess, assignResult.Error.Description);
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
                await dbContext.FeeRules.CountAsync());
        });
    }

    private sealed record SeedCounts(
        int Roles,
        int Users,
        int Shops,
        int FeeRules);
}
