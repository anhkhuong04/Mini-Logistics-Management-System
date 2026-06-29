using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using Xunit;

namespace MiniLogistics.Application.Tests;

public sealed class PartnerApiServiceTests
{
    private const string RawApiKey = "ml_test_partner_key_123456";

    private readonly Guid _ownerUserId = Guid.NewGuid();

    [Fact]
    public async Task Authenticate_ActiveApiClient_ReturnsContextAndMarksLastUsed()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClient = CreateApiClient(shop.Id);
        var repository = new FakeApiClientRepository([apiClient]);
        var service = new PartnerApiAuthenticationService(repository);

        var result = await service.AuthenticateAsync($"Bearer {RawApiKey}");

        Assert.True(result.IsSuccess);
        Assert.Equal(apiClient.Id, result.Value.ApiClientId);
        Assert.Equal(shop.Id, result.Value.ShopId);
        Assert.Equal(apiClient.Name, result.Value.Name);
        Assert.NotNull(apiClient.LastUsedAtUtc);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Authenticate_InactiveApiClient_IsRejected()
    {
        var apiClient = CreateApiClient(Guid.NewGuid());
        apiClient.Deactivate();
        var repository = new FakeApiClientRepository([apiClient]);
        var service = new PartnerApiAuthenticationService(repository);

        var result = await service.AuthenticateAsync($"Bearer {RawApiKey}");

        Assert.True(result.IsFailure);
        Assert.Equal(PartnerApiErrors.ApiClientInactive.Code, result.Error.Code);
        Assert.Null(apiClient.LastUsedAtUtc);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Quote_UsesShopPickupAddressAndCalculatesFee()
    {
        var shop = CreateShop(_ownerUserId);
        var service = CreateQuoteService(shop);

        var result = await service.QuoteAsync(new PartnerQuoteCommand(
            Guid.NewGuid(),
            shop.Id,
            PickupAddress: null,
            DeliveryAddress: new ShipmentAddressDto("9 Le Loi", "Hoan Kiem", "Ha Noi", "Vietnam"),
            WeightKg: 1.2m,
            LengthCm: 20m,
            WidthCm: 15m,
            HeightCm: 10m,
            GoodsValueAmount: 2_000_000m,
            CodAmount: 150_000m));

        Assert.True(result.IsSuccess);
        Assert.Equal(RouteType.InterRegion, result.Value.RouteType);
        Assert.Equal(1.2m, result.Value.ActualWeightKg);
        Assert.Equal(0.6m, result.Value.VolumetricWeightKg);
        Assert.Equal(1.2m, result.Value.ChargeableWeightKg);
        Assert.Equal(35_000m, result.Value.BaseFeeAmount);
        Assert.Equal(8_000m, result.Value.ExtraWeightFeeAmount);
        Assert.Equal(10_000m, result.Value.InsuranceFeeAmount);
        Assert.Equal(53_000m, result.Value.TotalFeeAmount);
        Assert.Equal("VND", result.Value.Currency);
    }

    [Fact]
    public async Task CreateShipment_CreatesShipmentCodAndExternalReference()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var service = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);

        var result = await service.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsIdempotentReplay);
        Assert.Equal("ECOM-10001", result.Value.Shipment.ExternalOrderId);
        Assert.Equal(RouteType.InterRegion, result.Value.Shipment.RouteType);
        Assert.Equal(53_000m, result.Value.Shipment.ShippingFeeAmount);
        Assert.Single(shipmentRepository.Shipments);
        Assert.Single(codTransactionRepository.CodTransactions);
        Assert.Single(referenceRepository.References);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);

        var shipment = shipmentRepository.Shipments.Single();
        var codTransaction = codTransactionRepository.CodTransactions.Single();
        var reference = referenceRepository.References.Single();
        Assert.Equal(shop.Id, shipment.ShopId);
        Assert.Equal(shop.Name, shipment.SenderName);
        Assert.Equal(_ownerUserId, shipment.StatusHistory.Single().ChangedByUserId);
        Assert.Equal(shipment.Id, codTransaction.ShipmentId);
        Assert.Equal(CodStatus.PendingCollection, codTransaction.Status);
        Assert.Equal(apiClientId, reference.ApiClientId);
        Assert.Equal(shop.Id, reference.ShopId);
        Assert.Equal(shipment.Id, reference.ShipmentId);
        Assert.Equal("ECOM-10001", reference.ExternalOrderId);
        Assert.Equal("idem-10001", reference.IdempotencyKey);
    }

    [Fact]
    public async Task CreateShipment_RetrySameIdempotencyKey_ReturnsStoredSnapshotWithoutCreatingDuplicate()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var service = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
        var command = CreateShipmentCommand(apiClientId, shop.Id);

        var firstResult = await service.CreateAsync(command);
        var retryResult = await service.CreateAsync(command);

        Assert.True(firstResult.IsSuccess);
        Assert.True(retryResult.IsSuccess);
        Assert.False(firstResult.Value.IsIdempotentReplay);
        Assert.True(retryResult.Value.IsIdempotentReplay);
        Assert.Equal(firstResult.Value.Shipment.ShipmentId, retryResult.Value.Shipment.ShipmentId);
        Assert.Equal(firstResult.Value.Shipment.TrackingCode, retryResult.Value.Shipment.TrackingCode);
        Assert.Single(shipmentRepository.Shipments);
        Assert.Single(codTransactionRepository.CodTransactions);
        Assert.Single(referenceRepository.References);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
    }

    [Fact]
    public async Task CreateShipment_SameIdempotencyKeyWithDifferentBody_ReturnsConflict()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var service = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);

        var firstResult = await service.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var conflictResult = await service.CreateAsync(CreateShipmentCommand(
            apiClientId,
            shop.Id,
            codAmount: 160_000m));

        Assert.True(firstResult.IsSuccess);
        Assert.True(conflictResult.IsFailure);
        Assert.Equal(PartnerApiErrors.IdempotencyConflict.Code, conflictResult.Error.Code);
        Assert.Single(shipmentRepository.Shipments);
        Assert.Single(codTransactionRepository.CodTransactions);
        Assert.Single(referenceRepository.References);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
    }

    [Fact]
    public async Task CreateShipment_SameExternalOrderWithDifferentIdempotencyKey_ReturnsConflict()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var service = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);

        var firstResult = await service.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var conflictResult = await service.CreateAsync(CreateShipmentCommand(
            apiClientId,
            shop.Id,
            idempotencyKey: "idem-other"));

        Assert.True(firstResult.IsSuccess);
        Assert.True(conflictResult.IsFailure);
        Assert.Equal("Application.Conflict", conflictResult.Error.Code);
        Assert.Single(shipmentRepository.Shipments);
        Assert.Single(codTransactionRepository.CodTransactions);
        Assert.Single(referenceRepository.References);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
    }

    [Fact]
    public async Task GetShipment_ReturnsTrackingForApiClientShipment()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var createService = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
        var createResult = await createService.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var queryService = CreateShipmentQueryService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);

        var result = await queryService.GetAsync(new PartnerGetShipmentCommand(
            apiClientId,
            shop.Id,
            createResult.Value.Shipment.TrackingCode));

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(createResult.Value.Shipment.TrackingCode, result.Value.TrackingCode);
        Assert.Equal("ECOM-10001", result.Value.ExternalOrderId);
        Assert.Equal(ShipmentStatus.PendingPickup, result.Value.Status);
        Assert.Equal(CodStatus.PendingCollection, result.Value.CodStatus);
        Assert.Equal(53_000m, result.Value.ShippingFeeAmount);
        Assert.Single(result.Value.Timeline);
        Assert.Equal(ShipmentStatus.PendingPickup, result.Value.Timeline.Single().Status);
    }

    [Fact]
    public async Task GetShipment_OtherShopCannotAccessShipment()
    {
        var shop = CreateShop(_ownerUserId);
        var otherShop = CreateShop(Guid.NewGuid());
        var apiClientId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var createService = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
        var createResult = await createService.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var queryService = CreateShipmentQueryService(
            [shop, otherShop],
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);

        var result = await queryService.GetAsync(new PartnerGetShipmentCommand(
            apiClientId,
            otherShop.Id,
            createResult.Value.Shipment.TrackingCode));

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Application.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetShipment_OtherApiClientOnSameShopCannotAccessReference()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var otherApiClientId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var createService = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
        var createResult = await createService.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var queryService = CreateShipmentQueryService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);

        var result = await queryService.GetAsync(new PartnerGetShipmentCommand(
            otherApiClientId,
            shop.Id,
            createResult.Value.Shipment.TrackingCode));

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Application.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task CancelShipment_CancelsPendingShipmentAndReturnsUpdatedTracking()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var createService = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
        var createResult = await createService.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var cancelService = CreateCancelShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);

        var result = await cancelService.CancelAsync(new PartnerCancelShipmentCommand(
            apiClientId,
            shop.Id,
            createResult.Value.Shipment.TrackingCode,
            "Customer cancelled order"));

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.Equal(ShipmentStatus.Cancelled, result.Value.Status);
        Assert.Equal(CodStatus.PendingCollection, result.Value.CodStatus);
        Assert.Equal(2, result.Value.Timeline.Count);
        Assert.Equal(ShipmentStatus.Cancelled, result.Value.Timeline.Last().Status);
        Assert.Equal(2, shipmentRepository.SaveChangesCount);

        var shipment = shipmentRepository.Shipments.Single();
        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
        Assert.Contains(shipment.StatusHistory, history =>
            history.Status == ShipmentStatus.Cancelled
            && history.ChangedByUserId == _ownerUserId
            && history.Note == "Customer cancelled order");
    }

    [Fact]
    public async Task CancelShipment_OtherShopCannotAccessShipment()
    {
        var shop = CreateShop(_ownerUserId);
        var otherShop = CreateShop(Guid.NewGuid());
        var apiClientId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var createService = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
        var createResult = await createService.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var cancelService = CreateCancelShipmentService(
            [shop, otherShop],
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);

        var result = await cancelService.CancelAsync(new PartnerCancelShipmentCommand(
            apiClientId,
            otherShop.Id,
            createResult.Value.Shipment.TrackingCode,
            "Customer cancelled order"));

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Application.NotFound", result.Error.Code);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
        Assert.Equal(ShipmentStatus.PendingPickup, shipmentRepository.Shipments.Single().Status);
    }

    [Fact]
    public async Task CancelShipment_WhenShipmentCannotBeCancelled_ReturnsConflict()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var shipperId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var createService = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
        var createResult = await createService.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var shipment = shipmentRepository.Shipments.Single();
        Assert.True(shipment.AssignShipper(shipperId, _ownerUserId, "Assign for pickup.").IsSuccess);
        Assert.True(shipment.UpdateStatus(ShipmentStatus.PickingUp, shipperId, "Picking up.").IsSuccess);
        Assert.True(shipment.UpdateStatus(ShipmentStatus.PickedUp, shipperId, "Picked up.").IsSuccess);
        var cancelService = CreateCancelShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);

        var result = await cancelService.CancelAsync(new PartnerCancelShipmentCommand(
            apiClientId,
            shop.Id,
            createResult.Value.Shipment.TrackingCode,
            "Customer cancelled order"));

        Assert.True(createResult.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(ShipmentErrors.CannotCancel.Code, result.Error.Code);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
        Assert.Equal(ShipmentStatus.PickedUp, shipment.Status);
    }

    private static PartnerQuoteService CreateQuoteService(Shop shop)
    {
        return new PartnerQuoteService(
            new PartnerQuoteCommandValidator(),
            new FakeShopRepository([shop]),
            new RouteClassificationService(),
            CreateShippingFeeService());
    }

    private static PartnerCreateShipmentService CreateShipmentService(
        Shop shop,
        FakeShipmentRepository shipmentRepository,
        FakeCodTransactionRepository codTransactionRepository,
        FakeExternalShipmentReferenceRepository referenceRepository)
    {
        return new PartnerCreateShipmentService(
            new PartnerCreateShipmentCommandValidator(),
            new FakeShopRepository([shop]),
            new RouteClassificationService(),
            CreateShippingFeeService(),
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
    }

    private static PartnerShipmentQueryService CreateShipmentQueryService(
        Shop shop,
        FakeShipmentRepository shipmentRepository,
        FakeCodTransactionRepository codTransactionRepository,
        FakeExternalShipmentReferenceRepository referenceRepository)
    {
        return CreateShipmentQueryService(
            [shop],
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
    }

    private static PartnerShipmentQueryService CreateShipmentQueryService(
        IReadOnlyList<Shop> shops,
        FakeShipmentRepository shipmentRepository,
        FakeCodTransactionRepository codTransactionRepository,
        FakeExternalShipmentReferenceRepository referenceRepository)
    {
        return new PartnerShipmentQueryService(
            new PartnerGetShipmentCommandValidator(),
            new FakeShopRepository(shops),
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
    }

    private static PartnerCancelShipmentService CreateCancelShipmentService(
        Shop shop,
        FakeShipmentRepository shipmentRepository,
        FakeCodTransactionRepository codTransactionRepository,
        FakeExternalShipmentReferenceRepository referenceRepository)
    {
        return CreateCancelShipmentService(
            [shop],
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
    }

    private static PartnerCancelShipmentService CreateCancelShipmentService(
        IReadOnlyList<Shop> shops,
        FakeShipmentRepository shipmentRepository,
        FakeCodTransactionRepository codTransactionRepository,
        FakeExternalShipmentReferenceRepository referenceRepository)
    {
        return new PartnerCancelShipmentService(
            new PartnerCancelShipmentCommandValidator(),
            new FakeShopRepository(shops),
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
    }

    private static ShippingFeeService CreateShippingFeeService()
    {
        return new ShippingFeeService(new FakeFeeRuleRepository([
            new FeeRule(
                RouteType.InterRegion,
                1m,
                new Money(35_000m),
                0.5m,
                new Money(8_000m))
        ]));
    }

    private static Shop CreateShop(Guid ownerUserId)
    {
        return new Shop(
            ownerUserId,
            "Demo Mini Shop",
            new PhoneNumber("0900000001"),
            new Address("123 Nguyen Trai", "Ben Thanh", "Ho Chi Minh"));
    }

    private static ApiClient CreateApiClient(Guid shopId)
    {
        return new ApiClient(
            shopId,
            "Demo E-commerce Integration",
            ApiKeyHasher.GetPrefix(RawApiKey),
            ApiKeyHasher.Hash(RawApiKey));
    }

    private static PartnerCreateShipmentCommand CreateShipmentCommand(
        Guid apiClientId,
        Guid shopId,
        string idempotencyKey = "idem-10001",
        decimal codAmount = 150_000m)
    {
        return new PartnerCreateShipmentCommand(
            apiClientId,
            shopId,
            "ECOM-10001",
            idempotencyKey,
            SenderName: null,
            SenderPhone: null,
            ReceiverName: "Nguyen Van A",
            ReceiverPhone: "0911111111",
            PickupAddress: null,
            DeliveryAddress: new ShipmentAddressDto("9 Le Loi", "Hoan Kiem", "Ha Noi", "Vietnam"),
            WeightKg: 1.2m,
            LengthCm: 20m,
            WidthCm: 15m,
            HeightCm: 10m,
            GoodsValueAmount: 2_000_000m,
            CodAmount: codAmount,
            Currency: "VND",
            Note: "Hang de vo, giao gio hanh chinh");
    }

    private sealed class FakeApiClientRepository : IApiClientRepository
    {
        private readonly List<ApiClient> _apiClients;

        public FakeApiClientRepository(IReadOnlyList<ApiClient> apiClients)
        {
            _apiClients = apiClients.ToList();
        }

        public int SaveChangesCount { get; private set; }

        public Task<ApiClient?> GetByApiKeyHashAsync(
            string apiKeyHash,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apiClients.FirstOrDefault(apiClient => apiClient.ApiKeyHash == apiKeyHash));
        }

        public Task AddAsync(ApiClient apiClient, CancellationToken cancellationToken = default)
        {
            _apiClients.Add(apiClient);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeExternalShipmentReferenceRepository : IExternalShipmentReferenceRepository
    {
        private readonly List<ExternalShipmentReference> _references;

        public FakeExternalShipmentReferenceRepository(IReadOnlyList<ExternalShipmentReference> references)
        {
            _references = references.ToList();
        }

        public IReadOnlyList<ExternalShipmentReference> References => _references.AsReadOnly();

        public Task<ExternalShipmentReference?> GetByApiClientAndIdempotencyKeyAsync(
            Guid apiClientId,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_references.FirstOrDefault(reference =>
                reference.ApiClientId == apiClientId
                && reference.IdempotencyKey == idempotencyKey));
        }

        public Task<ExternalShipmentReference?> GetByApiClientAndExternalOrderIdAsync(
            Guid apiClientId,
            string externalOrderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_references.FirstOrDefault(reference =>
                reference.ApiClientId == apiClientId
                && reference.ExternalOrderId == externalOrderId));
        }

        public Task<ExternalShipmentReference?> GetByApiClientAndShipmentIdAsync(
            Guid apiClientId,
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_references.FirstOrDefault(reference =>
                reference.ApiClientId == apiClientId
                && reference.ShipmentId == shipmentId));
        }

        public Task AddAsync(ExternalShipmentReference reference, CancellationToken cancellationToken = default)
        {
            _references.Add(reference);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeShopRepository : IShopRepository
    {
        private readonly List<Shop> _shops;

        public FakeShopRepository(IReadOnlyList<Shop> shops)
        {
            _shops = shops.ToList();
        }

        public int SaveChangesCount { get; private set; }

        public Task<Shop?> GetByIdAsync(
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.FirstOrDefault(shop => shop.Id == shopId));
        }

        public Task<Shop?> GetByOwnerUserIdAsync(
            Guid ownerUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.FirstOrDefault(shop => shop.OwnerUserId == ownerUserId));
        }

        public Task<bool> ExistsByOwnerUserIdAsync(
            Guid ownerUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shops.Any(shop => shop.OwnerUserId == ownerUserId));
        }

        public Task AddAsync(Shop shop, CancellationToken cancellationToken = default)
        {
            _shops.Add(shop);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeShipmentRepository : IShipmentRepository
    {
        private readonly List<Shipment> _shipments;

        public FakeShipmentRepository(IReadOnlyList<Shipment> shipments)
        {
            _shipments = shipments.ToList();
        }

        public int SaveChangesCount { get; private set; }

        public IReadOnlyList<Shipment> Shipments => _shipments.AsReadOnly();

        public Task<bool> ExistsByTrackingCodeAsync(
            TrackingCode trackingCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.Any(shipment => shipment.TrackingCode == trackingCode));
        }

        public Task<IReadOnlyList<Shipment>> GetByShopIdAsync(
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => shipment.ShopId == shopId).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByStatusAsync(
            ShipmentStatus status,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => shipment.Status == status).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByStatusesAsync(
            IReadOnlyCollection<ShipmentStatus> statuses,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => statuses.Contains(shipment.Status)).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetByIdsAsync(
            IReadOnlyCollection<Guid> shipmentIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments.Where(shipment => shipmentIds.Contains(shipment.Id)).ToList());
        }

        public Task<IReadOnlyList<Shipment>> GetAssignedToShipperAsync(
            Guid shipperId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shipment>>(_shipments
                .Where(shipment => shipment.Assignments.Any(assignment => assignment.IsActive && assignment.ShipperId == shipperId))
                .Where(shipment => shipment.Status is not ShipmentStatus.Returned and not ShipmentStatus.Cancelled)
                .ToList());
        }

        public Task<Shipment?> GetByIdAndShopIdAsync(
            Guid shipmentId,
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment => shipment.Id == shipmentId && shipment.ShopId == shopId));
        }

        public Task<Shipment?> GetTrackedByIdAndShopIdAsync(
            Guid shipmentId,
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return GetByIdAndShopIdAsync(shipmentId, shopId, cancellationToken);
        }

        public Task<Shipment?> GetTrackedByIdAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment => shipment.Id == shipmentId));
        }

        public Task<Shipment?> GetByTrackingCodeAsync(
            TrackingCode trackingCode,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment => shipment.TrackingCode == trackingCode));
        }

        public Task<Shipment?> GetByTrackingCodeAndShopIdAsync(
            TrackingCode trackingCode,
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_shipments.FirstOrDefault(shipment =>
                shipment.TrackingCode == trackingCode && shipment.ShopId == shopId));
        }

        public Task<Shipment?> GetTrackedByTrackingCodeAndShopIdAsync(
            TrackingCode trackingCode,
            Guid shopId,
            CancellationToken cancellationToken = default)
        {
            return GetByTrackingCodeAndShopIdAsync(trackingCode, shopId, cancellationToken);
        }

        public Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default)
        {
            _shipments.Add(shipment);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCodTransactionRepository : ICodTransactionRepository
    {
        private readonly List<CodTransaction> _codTransactions;

        public FakeCodTransactionRepository(IReadOnlyList<CodTransaction> codTransactions)
        {
            _codTransactions = codTransactions.ToList();
        }

        public IReadOnlyList<CodTransaction> CodTransactions => _codTransactions.AsReadOnly();

        public Task<IReadOnlyList<CodTransaction>> GetByStatusesAsync(
            IReadOnlyCollection<CodStatus> statuses,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CodTransaction>>(_codTransactions
                .Where(codTransaction => statuses.Contains(codTransaction.Status))
                .ToList());
        }

        public Task<CodTransaction?> GetByShipmentIdAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_codTransactions.FirstOrDefault(codTransaction => codTransaction.ShipmentId == shipmentId));
        }

        public Task<CodTransaction?> GetTrackedByShipmentIdAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return GetByShipmentIdAsync(shipmentId, cancellationToken);
        }

        public Task AddAsync(CodTransaction codTransaction, CancellationToken cancellationToken = default)
        {
            _codTransactions.Add(codTransaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFeeRuleRepository : IFeeRuleRepository
    {
        private readonly List<FeeRule> _feeRules;

        public FakeFeeRuleRepository(IReadOnlyList<FeeRule> feeRules)
        {
            _feeRules = feeRules.ToList();
        }

        public Task<IReadOnlyCollection<FeeRule>> GetActiveRulesAsync(
            RouteType routeType,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<FeeRule> rules = _feeRules
                .Where(rule => rule.IsActive && rule.RouteType == routeType)
                .ToList();

            return Task.FromResult(rules);
        }
    }
}
