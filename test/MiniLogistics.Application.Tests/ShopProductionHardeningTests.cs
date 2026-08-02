using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.Shops.Audit;
using MiniLogistics.Application.Shops.Notifications;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shipments.ImportShipments;
using MiniLogistics.Domain.AdminAuditing;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class ShopProductionHardeningTests
{
    [Fact]
    public void PiiMasking_MasksPhonesAddressesAndSecretsInAuditJson()
    {
        var service = new PiiMaskingService();

        var masked = service.MaskSensitiveJson("{\"receiverPhone\":\"0912345678\",\"address\":\"1 Le Loi\",\"signingSecret\":\"raw\",\"status\":\"Delivered\"}");

        Assert.NotNull(masked);
        Assert.DoesNotContain("0912345678", masked);
        Assert.DoesNotContain("1 Le Loi", masked);
        Assert.DoesNotContain("raw", masked);
        Assert.Contains("Delivered", masked);
        Assert.Equal("091****678", service.MaskPhone("0912345678"));
        Assert.Equal("N*** V*** *", service.MaskName("Nguyen Van A"));
        Assert.Equal("***", service.MaskAddress("1 Le Loi"));
    }

    [Fact]
    public async Task ShopAuditService_UsesAccessibleShopScopeAndMasksReturnedValues()
    {
        var ownerId = Guid.NewGuid();
        var shop = CreateShop(ownerId);
        var log = new AdminAuditLog(
            ownerId,
            "Shop",
            "shipment.created",
            "Shipment",
            Guid.NewGuid(),
            TestClock.UtcNow,
            newValueJson: "{\"receiverPhone\":\"0912345678\"}");
        var repository = new CapturingAuditRepository([log]);
        var service = new GetShopAuditLogsService(
            new FakeShopAccessService(shop),
            repository,
            new PiiMaskingService());

        var result = await service.GetAsync(new ShopAuditLogQuery(ownerId, shop.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal([shop.Id], repository.LastShopIds);
        Assert.DoesNotContain("0912345678", result.Value.Single().NewValueJson);
    }

    [Fact]
    public async Task NotificationQueue_WhenOutboxFails_DoesNotThrow()
    {
        var ownerId = Guid.NewGuid();
        var shop = CreateShop(ownerId);
        var service = new ShopNotificationService(
            new FakeShopAccessService(shop),
            new EmptyNotificationRepository(),
            new ThrowingOutboxWriter(),
            TestClock.Provider,
            NullLogger<ShopNotificationService>.Instance);
        var shipment = Shipment.Create(
            shop.Id,
            "Sender",
            new PhoneNumber("0912345678"),
            "Receiver",
            new PhoneNumber("0987654321"),
            shop.Address,
            new Address("2 Le Loi", "Ben Thanh", "Ho Chi Minh"),
            new Weight(1),
            new ParcelDimensions(10, 10, 10),
            new Weight(1),
            new Money(100_000, "VND"),
            Money.Zero,
            new ShippingFeeBreakdown(Money.Zero, Money.Zero, Money.Zero, Money.Zero),
            RouteType.IntraProvince,
            ownerId,
            TestClock.UtcNow);

        var exception = await Record.ExceptionAsync(() => service.QueueShipmentEventAsync(
            shipment,
            ShopNotificationEventTypes.Delivered));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ImportBatchProcessor_CreatesShipmentAndCompletesBatch()
    {
        var ownerId = Guid.NewGuid();
        var shop = CreateShop(ownerId);
        var batch = new ShipmentImportBatch(shop.Id, ownerId, TestClock.UtcNow);
        var draft = new ShipmentImportRowDraft(
            2, "ORDER-1", "Receiver", "0987654321", "2 Le Loi", "Ben Thanh", "Ho Chi Minh", "Vietnam",
            1, 10, 10, 10, 100_000, 0, null);
        batch.AddRow(new ShipmentImportBatchRow(
            batch.Id, shop.Id, draft.RowNumber, draft.ClientOrderCode, JsonSerializer.Serialize(draft), true, TestClock.UtcNow));
        var repository = new FakeImportBatchRepository(batch);
        var shipmentId = Guid.NewGuid();
        var processor = new ShipmentImportBatchProcessor(
            repository,
            new FakeShopAccessService(shop),
            new SuccessfulCreateShipmentService(shipmentId),
            new FakeTransactionManager(),
            TestClock.Provider,
            NullLogger<ShipmentImportBatchProcessor>.Instance);

        var processed = await processor.ProcessNextRowAsync();

        Assert.True(processed);
        Assert.Equal(ShipmentImportBatchStatus.Completed, batch.Status);
        Assert.Equal(1, batch.CreatedRows);
        Assert.Equal(shipmentId, batch.Rows.Single().ShipmentId);
    }

    private static Shop CreateShop(Guid ownerId) => new(
        ownerId,
        "Shop",
        new PhoneNumber("0900000000"),
        new Address("1 Le Loi", "Ben Thanh", "Ho Chi Minh"),
        TestClock.UtcNow);

    private sealed class FakeShopAccessService(Shop shop) : IShopAccessService
    {
        public Task<Result<IReadOnlyList<Shop>>> GetAccessibleShopsAsync(Guid currentUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<IReadOnlyList<Shop>>.Success([shop]));

        public Task<Result<Shop>> GetShopForUserAsync(Guid currentUserId, Guid? shopId, bool requireActiveShop, CancellationToken cancellationToken = default) =>
            Task.FromResult(shopId is null || shopId == shop.Id
                ? Result<Shop>.Success(shop)
                : Result<Shop>.Failure(ApplicationErrors.Forbidden("No access.")));
    }

    private sealed class CapturingAuditRepository(IReadOnlyList<AdminAuditLog> logs) : IShopAuditLogRepository
    {
        public IReadOnlyCollection<Guid> LastShopIds { get; private set; } = [];

        public Task<IReadOnlyList<AdminAuditLog>> QueryForShopsAsync(Guid currentUserId, IReadOnlyCollection<Guid> shopIds, ShopAuditLogQuery query, CancellationToken cancellationToken = default)
        {
            LastShopIds = shopIds;
            return Task.FromResult(logs);
        }
    }

    private sealed class ThrowingOutboxWriter : IOutboxWriter
    {
        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("outbox unavailable");
    }

    private sealed class EmptyNotificationRepository : IShopNotificationRepository
    {
        public Task<bool> ExistsAsync(Guid notificationId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(ShopNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ShopNotification>> GetRecentAsync(Guid shopId, Guid userId, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ShopNotification>>([]);
        public Task<ShopNotification?> GetByIdAsync(Guid notificationId, Guid shopId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<ShopNotification?>(null);
        public Task<ShopNotificationPreference?> GetPreferenceAsync(Guid shopId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<ShopNotificationPreference?>(null);
        public Task AddPreferenceAsync(ShopNotificationPreference preference, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeImportBatchRepository(ShipmentImportBatch batch) : IShipmentImportBatchRepository
    {
        public Task AddAsync(ShipmentImportBatch value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ShipmentImportBatch?> GetByIdForShopAsync(Guid batchId, Guid shopId, CancellationToken cancellationToken = default) => Task.FromResult<ShipmentImportBatch?>(batch);
        public Task<ShipmentImportBatch?> GetNextProcessableAsync(CancellationToken cancellationToken = default) => Task.FromResult<ShipmentImportBatch?>(batch.Status is ShipmentImportBatchStatus.Pending or ShipmentImportBatchStatus.Processing ? batch : null);
        public Task<bool> HasCreatedClientOrderAsync(Guid shopId, string clientOrderCode, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SuccessfulCreateShipmentService(Guid shipmentId) : ICreateShipmentService
    {
        public Task<Result<CreateShipmentResponse>> CreateAsync(CreateShipmentCommand command, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<CreateShipmentResponse>.Success(new CreateShipmentResponse(
                shipmentId, "ML202608020001", 1, 1, 1, 0, 0, 0, 0, 0, "VND", ShipmentStatus.PendingPickup)));
    }

    private sealed class FakeTransactionManager : IApplicationDbTransactionManager
    {
        public Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IApplicationDbTransaction>(new FakeTransaction());

        private sealed class FakeTransaction : IApplicationDbTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
