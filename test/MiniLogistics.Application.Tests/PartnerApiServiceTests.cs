using System.Text.Json;
using System.Text;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Outbox;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Users;
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
        var service = new PartnerApiAuthenticationService(repository, new FakeShopRepository([shop]));

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
        var service = new PartnerApiAuthenticationService(repository, new FakeShopRepository([]));

        var result = await service.AuthenticateAsync($"Bearer {RawApiKey}");

        Assert.True(result.IsFailure);
        Assert.Equal(PartnerApiErrors.ApiClientInactive.Code, result.Error.Code);
        Assert.Null(apiClient.LastUsedAtUtc);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Authenticate_InactiveShop_IsRejected()
    {
        var shop = CreateShop(_ownerUserId);
        shop.Deactivate();
        var apiClient = CreateApiClient(shop.Id);
        var repository = new FakeApiClientRepository([apiClient]);
        var service = new PartnerApiAuthenticationService(repository, new FakeShopRepository([shop]));

        var result = await service.AuthenticateAsync($"Bearer {RawApiKey}");

        Assert.True(result.IsFailure);
        Assert.Equal(PartnerApiErrors.ShopInactive.Code, result.Error.Code);
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
    public async Task CreateShipment_WithWebhookEndpoint_WritesOutboxInSameSave()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var endpoint = new WebhookEndpoint(
            apiClientId,
            "https://partner.example.test/webhooks/minilogistics",
            "protected-secret");
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var outboxRepository = new FakeOutboxMessageRepository([]);
        var publisher = new WebhookEventPublisher(
            referenceRepository,
            new FakeWebhookEndpointRepository([endpoint]),
            outboxRepository);
        var service = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository,
            webhookEventPublisher: publisher);

        var result = await service.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));

        Assert.True(result.IsSuccess);
        Assert.Single(shipmentRepository.Shipments);
        Assert.Single(codTransactionRepository.CodTransactions);
        Assert.Single(referenceRepository.References);
        Assert.Single(outboxRepository.Messages);
        Assert.Equal(1, shipmentRepository.SaveChangesCount);
        Assert.Equal(0, outboxRepository.SaveChangesCount);

        var message = outboxRepository.Messages.Single();
        Assert.Equal(OutboxMessageTypes.WebhookShipmentCreated, message.Type);
        Assert.Equal(shipmentRepository.Shipments.Single().Id, message.AggregateId);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
    }

    [Fact]
    public async Task CreateShipment_AutoAssignUpdatesResponseAndIdempotencySnapshot()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var shipperId = Guid.NewGuid();
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var service = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository,
            new AssigningAutoAssignShipmentService(shipmentRepository, shipperId));

        var firstResult = await service.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var replayResult = await service.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));

        Assert.True(firstResult.IsSuccess);
        Assert.True(replayResult.IsSuccess);
        Assert.False(firstResult.Value.IsIdempotentReplay);
        Assert.True(replayResult.Value.IsIdempotentReplay);
        Assert.Equal(ShipmentStatus.Assigned, firstResult.Value.Shipment.Status);
        Assert.Equal(ShipmentStatus.Assigned, replayResult.Value.Shipment.Status);

        var shipment = shipmentRepository.Shipments.Single();
        Assert.Equal(ShipmentStatus.Assigned, shipment.Status);
        Assert.Contains(shipment.Assignments, assignment => assignment.IsActive && assignment.ShipperId == shipperId);

        var snapshot = JsonSerializer.Deserialize<PartnerShipmentResponse>(
            referenceRepository.References.Single().ResponseSnapshotJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(snapshot);
        Assert.Equal(ShipmentStatus.Assigned, snapshot.Status);
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
    public async Task CancelShipment_WithWebhookEndpoint_EnqueuesStatusChangedOutboxMessage()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientId = Guid.NewGuid();
        var endpoint = new WebhookEndpoint(
            apiClientId,
            "https://partner.example.test/webhooks/minilogistics",
            "secret-for-signing");
        var shipmentRepository = new FakeShipmentRepository([]);
        var codTransactionRepository = new FakeCodTransactionRepository([]);
        var referenceRepository = new FakeExternalShipmentReferenceRepository([]);
        var endpointRepository = new FakeWebhookEndpointRepository([endpoint]);
        var outboxRepository = new FakeOutboxMessageRepository([]);
        var publisher = new WebhookEventPublisher(
            referenceRepository,
            endpointRepository,
            outboxRepository);
        var createService = CreateShipmentService(
            shop,
            shipmentRepository,
            codTransactionRepository,
            referenceRepository);
        var createResult = await createService.CreateAsync(CreateShipmentCommand(apiClientId, shop.Id));
        var cancelService = new PartnerCancelShipmentService(
            new PartnerCancelShipmentCommandValidator(),
            new FakeShopRepository([shop]),
            shipmentRepository,
            codTransactionRepository,
            referenceRepository,
            publisher);

        var cancelResult = await cancelService.CancelAsync(new PartnerCancelShipmentCommand(
            apiClientId,
            shop.Id,
            createResult.Value.Shipment.TrackingCode,
            "Customer cancelled order"));

        Assert.True(createResult.IsSuccess);
        Assert.True(cancelResult.IsSuccess);
        Assert.Single(outboxRepository.Messages);

        var message = outboxRepository.Messages.Single();
        Assert.Equal(OutboxMessageTypes.WebhookShipmentStatusChanged, message.Type);
        Assert.Equal(shipmentRepository.Shipments.Single().Id, message.AggregateId);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        var outboxPayload = JsonSerializer.Deserialize<WebhookDeliveryOutboxPayload>(
            message.PayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(outboxPayload);
        Assert.Equal(endpoint.Id, outboxPayload.WebhookEndpointId);
        Assert.Equal(apiClientId, outboxPayload.ApiClientId);
        Assert.Equal(WebhookEventTypes.ShipmentStatusChanged, outboxPayload.EventType);

        var payload = JsonSerializer.Deserialize<WebhookShipmentPayload>(
            outboxPayload.WebhookPayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.Equal(message.Id, payload.EventId);
        Assert.Equal(WebhookEventTypes.ShipmentStatusChanged, payload.Event);
        Assert.Equal("ECOM-10001", payload.ExternalOrderId);
        Assert.Equal(createResult.Value.Shipment.TrackingCode, payload.TrackingCode);
        Assert.Equal(nameof(ShipmentStatus.Cancelled), payload.Status);
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

    [Fact]
    public void WebhookSignature_UsesTimestampPayloadAndSecret()
    {
        const string timestamp = "2026-06-29T10:30:00.0000000Z";
        const string payload = "{\"event\":\"shipment.status_changed\"}";

        var signature = WebhookSignature.Compute("secret", timestamp, payload);
        var sameSignature = WebhookSignature.Compute("secret", timestamp, payload);
        var changedPayloadSignature = WebhookSignature.Compute("secret", timestamp, "{\"event\":\"shipment.created\"}");

        Assert.StartsWith("sha256=", signature);
        Assert.Equal(signature, sameSignature);
        Assert.NotEqual(signature, changedPayloadSignature);
    }

    [Fact]
    public async Task IntegrationManagement_ShopCanCreateApiClientForOwnShop()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClientRepository = new FakeApiClientRepository([]);
        var service = CreateIntegrationManagementService(
            FakeIdentityService.For(_ownerUserId, UserRole.Shop),
            new FakeShopRepository([shop]),
            apiClientRepository,
            new FakeWebhookEndpointRepository([]),
            new FakeWebhookDeliveryRepository([]));

        var result = await service.CreateApiClientAsync(new CreatePartnerApiClientCommand(
            _ownerUserId,
            shop.Id,
            "WooCommerce Store"));

        Assert.True(result.IsSuccess);
        Assert.StartsWith("ml_live_", result.Value.ApiKey);
        Assert.Single(apiClientRepository.ApiClients);
        Assert.Equal(result.Value.ApiClientId, apiClientRepository.ApiClients.Single().Id);
        Assert.Equal(ApiKeyHasher.Hash(result.Value.ApiKey), apiClientRepository.ApiClients.Single().ApiKeyHash);
        Assert.Equal(1, apiClientRepository.SaveChangesCount);
    }

    [Fact]
    public async Task IntegrationManagement_ShopDashboardIncludesAllOwnedShops()
    {
        var firstShop = CreateShop(_ownerUserId);
        firstShop.Rename("First Shop");
        var secondShop = CreateShop(_ownerUserId);
        secondShop.Rename("Second Shop");
        var otherShop = CreateShop(Guid.NewGuid());
        var firstClient = CreateApiClient(firstShop.Id);
        var secondClient = CreateApiClient(secondShop.Id);
        var otherClient = CreateApiClient(otherShop.Id);
        var service = CreateIntegrationManagementService(
            FakeIdentityService.For(_ownerUserId, UserRole.Shop),
            new FakeShopRepository([firstShop, secondShop, otherShop]),
            new FakeApiClientRepository([firstClient, secondClient, otherClient]),
            new FakeWebhookEndpointRepository([]),
            new FakeWebhookDeliveryRepository([]));

        var result = await service.GetDashboardAsync(_ownerUserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Shops.Count);
        Assert.Contains(result.Value.Shops, shop => shop.ShopId == firstShop.Id && shop.ShopName == "First Shop");
        Assert.Contains(result.Value.Shops, shop => shop.ShopId == secondShop.Id && shop.ShopName == "Second Shop");
        Assert.DoesNotContain(result.Value.Shops, shop => shop.ShopId == otherShop.Id);
        Assert.Equal(2, result.Value.ApiClients.Count);
        Assert.Contains(result.Value.ApiClients, client => client.ApiClientId == firstClient.Id);
        Assert.Contains(result.Value.ApiClients, client => client.ApiClientId == secondClient.Id);
        Assert.DoesNotContain(result.Value.ApiClients, client => client.ApiClientId == otherClient.Id);
    }

    [Fact]
    public async Task IntegrationManagement_ShopCannotCreateApiClientForOtherShop()
    {
        var shop = CreateShop(_ownerUserId);
        var otherShop = CreateShop(Guid.NewGuid());
        var apiClientRepository = new FakeApiClientRepository([]);
        var service = CreateIntegrationManagementService(
            FakeIdentityService.For(_ownerUserId, UserRole.Shop),
            new FakeShopRepository([shop, otherShop]),
            apiClientRepository,
            new FakeWebhookEndpointRepository([]),
            new FakeWebhookDeliveryRepository([]));

        var result = await service.CreateApiClientAsync(new CreatePartnerApiClientCommand(
            _ownerUserId,
            otherShop.Id,
            "Forbidden store"));

        Assert.True(result.IsFailure);
        Assert.Equal("Application.Forbidden", result.Error.Code);
        Assert.Empty(apiClientRepository.ApiClients);
    }

    [Fact]
    public async Task IntegrationManagement_InactiveShopIsReadableButCannotCreateApiClient()
    {
        var shop = CreateShop(_ownerUserId);
        shop.Deactivate();
        var apiClientRepository = new FakeApiClientRepository([]);
        var service = CreateIntegrationManagementService(
            FakeIdentityService.For(_ownerUserId, UserRole.Shop),
            new FakeShopRepository([shop]),
            apiClientRepository,
            new FakeWebhookEndpointRepository([]),
            new FakeWebhookDeliveryRepository([]));

        var dashboardResult = await service.GetDashboardAsync(_ownerUserId);
        var createResult = await service.CreateApiClientAsync(new CreatePartnerApiClientCommand(
            _ownerUserId,
            shop.Id,
            "Inactive store"));

        Assert.True(dashboardResult.IsSuccess);
        Assert.Contains(dashboardResult.Value.Shops, item => item.ShopId == shop.Id && !item.IsActive);
        Assert.True(createResult.IsFailure);
        Assert.Equal("Application.Forbidden", createResult.Error.Code);
        Assert.Empty(apiClientRepository.ApiClients);
    }

    [Fact]
    public async Task IntegrationManagement_AdminDashboardIncludesAllShops()
    {
        var adminId = Guid.NewGuid();
        var firstShop = CreateShop(_ownerUserId);
        var secondShop = CreateShop(Guid.NewGuid());
        var firstClient = CreateApiClient(firstShop.Id);
        var secondClient = CreateApiClient(secondShop.Id);
        var service = CreateIntegrationManagementService(
            FakeIdentityService.For(adminId, UserRole.Admin),
            new FakeShopRepository([firstShop, secondShop]),
            new FakeApiClientRepository([firstClient, secondClient]),
            new FakeWebhookEndpointRepository([]),
            new FakeWebhookDeliveryRepository([]));

        var result = await service.GetDashboardAsync(adminId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Shops.Count);
        Assert.Contains(result.Value.Shops, shop => shop.ShopId == firstShop.Id);
        Assert.Contains(result.Value.Shops, shop => shop.ShopId == secondShop.Id);
        Assert.Equal(2, result.Value.ApiClients.Count);
    }

    [Fact]
    public async Task IntegrationManagement_CanRotateRevokeConfigureAndTestWebhook()
    {
        var shop = CreateShop(_ownerUserId);
        var apiClient = CreateApiClient(shop.Id);
        var apiClientRepository = new FakeApiClientRepository([apiClient]);
        var endpointRepository = new FakeWebhookEndpointRepository([]);
        var deliveryRepository = new FakeWebhookDeliveryRepository([]);
        var service = CreateIntegrationManagementService(
            FakeIdentityService.For(_ownerUserId, UserRole.Shop),
            new FakeShopRepository([shop]),
            apiClientRepository,
            endpointRepository,
            deliveryRepository);

        var rotateResult = await service.RotateApiClientKeyAsync(new RotatePartnerApiClientKeyCommand(
            _ownerUserId,
            apiClient.Id));
        var revokeResult = await service.SetApiClientActiveStatusAsync(new SetPartnerApiClientActiveStatusCommand(
            _ownerUserId,
            apiClient.Id,
            IsActive: false));
        var webhookResult = await service.UpsertWebhookEndpointAsync(new UpsertPartnerWebhookEndpointCommand(
            _ownerUserId,
            apiClient.Id,
            "https://partner.example.test/webhooks/minilogistics",
            "secret-for-signing"));
        var testResult = await service.TestWebhookAsync(new TestPartnerWebhookCommand(
            _ownerUserId,
            apiClient.Id));

        Assert.True(rotateResult.IsSuccess);
        Assert.NotEqual(RawApiKey, rotateResult.Value.ApiKey);
        Assert.Equal(ApiKeyHasher.Hash(rotateResult.Value.ApiKey), apiClient.ApiKeyHash);
        Assert.True(revokeResult.IsSuccess);
        Assert.False(apiClient.IsActive);
        Assert.True(webhookResult.IsSuccess);
        Assert.Single(endpointRepository.Endpoints);
        Assert.True(testResult.IsSuccess);
        Assert.Single(deliveryRepository.Deliveries);
        Assert.Equal(WebhookEventTypes.WebhookTest, deliveryRepository.Deliveries.Single().EventType);
        Assert.Equal(WebhookDeliveryStatus.Pending, deliveryRepository.Deliveries.Single().Status);
    }

    [Fact]
    public async Task IntegrationManagement_RotateKeyWritesAuditAndInvalidatesOldKey()
    {
        var shop = CreateShop(_ownerUserId);
        var shopRepository = new FakeShopRepository([shop]);
        var apiClient = CreateApiClient(shop.Id);
        var apiClientRepository = new FakeApiClientRepository([apiClient]);
        var credentialAuditRepository = new FakePartnerApiCredentialAuditRepository([]);
        var service = CreateIntegrationManagementService(
            FakeIdentityService.For(_ownerUserId, UserRole.Shop),
            shopRepository,
            apiClientRepository,
            new FakeWebhookEndpointRepository([]),
            new FakeWebhookDeliveryRepository([]),
            credentialAuditRepository);

        var rotateResult = await service.RotateApiClientKeyAsync(new RotatePartnerApiClientKeyCommand(
            _ownerUserId,
            apiClient.Id));
        var oldKeyAuthentication = await new PartnerApiAuthenticationService(apiClientRepository, shopRepository)
            .AuthenticateAsync($"Bearer {RawApiKey}");

        Assert.True(rotateResult.IsSuccess);
        Assert.True(oldKeyAuthentication.IsFailure);
        Assert.Equal(PartnerApiErrors.InvalidApiKey.Code, oldKeyAuthentication.Error.Code);
        var audit = Assert.Single(credentialAuditRepository.Audits);
        Assert.Equal(PartnerApiCredentialAuditActions.ApiClientKeyRotated, audit.Action);
        Assert.True(audit.IsSuccess);
        Assert.Equal(_ownerUserId, audit.ActorUserId);
        Assert.Equal(shop.Id, audit.ShopId);
        Assert.Equal(apiClient.Id, audit.ApiClientId);
    }

    [Fact]
    public async Task IntegrationManagement_DeactivateWritesAuditAndAuthenticationIsForbidden()
    {
        var shop = CreateShop(_ownerUserId);
        var shopRepository = new FakeShopRepository([shop]);
        var apiClient = CreateApiClient(shop.Id);
        var apiClientRepository = new FakeApiClientRepository([apiClient]);
        var credentialAuditRepository = new FakePartnerApiCredentialAuditRepository([]);
        var service = CreateIntegrationManagementService(
            FakeIdentityService.For(_ownerUserId, UserRole.Shop),
            shopRepository,
            apiClientRepository,
            new FakeWebhookEndpointRepository([]),
            new FakeWebhookDeliveryRepository([]),
            credentialAuditRepository);

        var deactivateResult = await service.SetApiClientActiveStatusAsync(new SetPartnerApiClientActiveStatusCommand(
            _ownerUserId,
            apiClient.Id,
            IsActive: false));
        var authenticationResult = await new PartnerApiAuthenticationService(apiClientRepository, shopRepository)
            .AuthenticateAsync($"Bearer {RawApiKey}");

        Assert.True(deactivateResult.IsSuccess);
        Assert.True(authenticationResult.IsFailure);
        Assert.Equal(PartnerApiErrors.ApiClientInactive.Code, authenticationResult.Error.Code);
        var audit = Assert.Single(credentialAuditRepository.Audits);
        Assert.Equal(PartnerApiCredentialAuditActions.ApiClientDeactivated, audit.Action);
        Assert.True(audit.IsSuccess);
        Assert.Equal(apiClient.Id, audit.ApiClientId);
    }

    [Fact]
    public async Task IntegrationManagement_WebhookSecretIsProtectedAndAuditDoesNotContainRawSecret()
    {
        const string rawSecret = "secret-for-signing";
        var shop = CreateShop(_ownerUserId);
        var apiClient = CreateApiClient(shop.Id);
        var endpointRepository = new FakeWebhookEndpointRepository([]);
        var credentialAuditRepository = new FakePartnerApiCredentialAuditRepository([]);
        var secretProtector = new FakeSecretProtector();
        var service = CreateIntegrationManagementService(
            FakeIdentityService.For(_ownerUserId, UserRole.Shop),
            new FakeShopRepository([shop]),
            new FakeApiClientRepository([apiClient]),
            endpointRepository,
            new FakeWebhookDeliveryRepository([]),
            credentialAuditRepository,
            secretProtector);

        var result = await service.UpsertWebhookEndpointAsync(new UpsertPartnerWebhookEndpointCommand(
            _ownerUserId,
            apiClient.Id,
            "https://partner.example.test/webhooks/minilogistics",
            rawSecret));

        Assert.True(result.IsSuccess);
        var endpoint = Assert.Single(endpointRepository.Endpoints);
        Assert.NotEqual(rawSecret, endpoint.ProtectedSigningSecret);
        Assert.DoesNotContain(rawSecret, endpoint.ProtectedSigningSecret, StringComparison.Ordinal);
        Assert.True(secretProtector.IsProtected(endpoint.ProtectedSigningSecret));
        Assert.Equal(rawSecret, secretProtector.Unprotect(endpoint.ProtectedSigningSecret));
        var audit = Assert.Single(credentialAuditRepository.Audits);
        Assert.Equal(PartnerApiCredentialAuditActions.WebhookEndpointUpserted, audit.Action);
        Assert.DoesNotContain(rawSecret, audit.Action, StringComparison.Ordinal);
        Assert.DoesNotContain(rawSecret, audit.ErrorCode ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(rawSecret, audit.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
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
        FakeExternalShipmentReferenceRepository referenceRepository,
        IAutoAssignShipmentService? autoAssignShipmentService = null,
        IWebhookEventPublisher? webhookEventPublisher = null)
    {
        return new PartnerCreateShipmentService(
            new PartnerCreateShipmentCommandValidator(),
            new FakeShopRepository([shop]),
            new RouteClassificationService(),
            CreateShippingFeeService(),
            shipmentRepository,
            codTransactionRepository,
            referenceRepository,
            autoAssignShipmentService ?? new NoOpAutoAssignShipmentService(shipmentRepository),
            webhookEventPublisher);
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

    private static PartnerIntegrationManagementService CreateIntegrationManagementService(
        IIdentityService identityService,
        FakeShopRepository shopRepository,
        FakeApiClientRepository apiClientRepository,
        FakeWebhookEndpointRepository endpointRepository,
        FakeWebhookDeliveryRepository deliveryRepository,
        FakePartnerApiCredentialAuditRepository? credentialAuditRepository = null,
        ISecretProtector? secretProtector = null)
    {
        return new PartnerIntegrationManagementService(
            identityService,
            shopRepository,
            apiClientRepository,
            endpointRepository,
            deliveryRepository,
            credentialAuditRepository ?? new FakePartnerApiCredentialAuditRepository([]),
            secretProtector ?? new FakeSecretProtector());
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

    private sealed class NoOpAutoAssignShipmentService : IAutoAssignShipmentService
    {
        private readonly FakeShipmentRepository _shipmentRepository;

        public NoOpAutoAssignShipmentService(FakeShipmentRepository shipmentRepository)
        {
            _shipmentRepository = shipmentRepository;
        }

        public Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default,
            Guid? requestedByUserId = null)
        {
            var shipment = _shipmentRepository.Shipments.First(shipment => shipment.Id == shipmentId);

            return Task.FromResult(Result<AutoAssignShipmentResult>.Success(
                AutoAssignShipmentResult.Skipped(shipment, "Auto assignment disabled for this test.")));
        }
    }

    private sealed class AssigningAutoAssignShipmentService : IAutoAssignShipmentService
    {
        private readonly FakeShipmentRepository _shipmentRepository;
        private readonly Guid _shipperId;

        public AssigningAutoAssignShipmentService(
            FakeShipmentRepository shipmentRepository,
            Guid shipperId)
        {
            _shipmentRepository = shipmentRepository;
            _shipperId = shipperId;
        }

        public async Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default,
            Guid? requestedByUserId = null)
        {
            var shipment = _shipmentRepository.Shipments.First(shipment => shipment.Id == shipmentId);
            var assignResult = shipment.AssignShipper(
                _shipperId,
                SystemActorIds.AutoAssignment,
                "Auto assigned from partner test.");
            if (assignResult.IsFailure)
            {
                return Result<AutoAssignShipmentResult>.Failure(assignResult.Error);
            }

            await _shipmentRepository.SaveChangesAsync(cancellationToken);

            return Result<AutoAssignShipmentResult>.Success(
                AutoAssignShipmentResult.Assigned(shipment, _shipperId, "Assigned by partner test."));
        }
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        private readonly Dictionary<Guid, (bool IsActive, HashSet<string> Roles)> _users = [];

        private FakeIdentityService()
        {
        }

        public static FakeIdentityService For(Guid userId, params UserRole[] roles)
        {
            var service = new FakeIdentityService();
            service._users[userId] = (true, roles.Select(role => role.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase));
            return service;
        }

        public Task<Result<Guid>> CreateUserAsync(
            string fullName,
            string email,
            string phoneNumber,
            string password,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> AddToRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<Guid>> CreateInternalUserAsync(
            string fullName,
            string email,
            string phoneNumber,
            string password,
            string role,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetUserActiveStatusAsync(
            Guid userId,
            bool isActive,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result> SetShipperCapacityAsync(
            Guid userId,
            bool isAvailableForAssignment,
            int maxActiveShipments,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Success());
        }

        public Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            if (!_users.TryGetValue(userId, out var user))
            {
                return Task.FromResult(new IdentityUserRoleCheckResponse(userId, false, false, false));
            }

            return Task.FromResult(new IdentityUserRoleCheckResponse(
                userId,
                true,
                user.IsActive,
                user.Roles.Contains(role)));
        }

        public Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IdentityUserWithRolesResponse>>([]);
        }

        public Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ActiveShipperResponse>>([]);
        }

        public Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<IdentityUserSummaryResponse>>([]);
        }
    }

    private sealed class FakePartnerApiCredentialAuditRepository : IPartnerApiCredentialAuditRepository
    {
        private readonly List<PartnerApiCredentialAudit> _audits;

        public FakePartnerApiCredentialAuditRepository(IReadOnlyList<PartnerApiCredentialAudit> audits)
        {
            _audits = audits.ToList();
        }

        public IReadOnlyList<PartnerApiCredentialAudit> Audits => _audits.AsReadOnly();

        public int SaveChangesCount { get; private set; }

        public Task<IReadOnlyList<PartnerApiCredentialAudit>> GetRecentByApiClientIdsAsync(
            IReadOnlyCollection<Guid> apiClientIds,
            int takePerClient,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PartnerApiCredentialAudit>>(_audits
                .Where(audit => audit.ApiClientId.HasValue && apiClientIds.Contains(audit.ApiClientId.Value))
                .GroupBy(audit => audit.ApiClientId)
                .SelectMany(group => group
                    .OrderByDescending(audit => audit.CreatedAtUtc)
                    .Take(takePerClient))
                .ToList());
        }

        public Task AddAsync(
            PartnerApiCredentialAudit audit,
            CancellationToken cancellationToken = default)
        {
            _audits.Add(audit);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        private const string Prefix = "fake:v1:";

        public string Protect(string plaintextSecret)
        {
            return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintextSecret));
        }

        public string Unprotect(string protectedSecret)
        {
            return IsProtected(protectedSecret)
                ? Encoding.UTF8.GetString(Convert.FromBase64String(protectedSecret[Prefix.Length..]))
                : protectedSecret;
        }

        public bool IsProtected(string value)
        {
            return value.StartsWith(Prefix, StringComparison.Ordinal);
        }
    }

    private sealed class FakeOutboxMessageRepository : IOutboxMessageRepository
    {
        private readonly List<OutboxMessage> _messages;

        public FakeOutboxMessageRepository(IReadOnlyList<OutboxMessage> messages)
        {
            _messages = messages.ToList();
        }

        public IReadOnlyList<OutboxMessage> Messages => _messages.AsReadOnly();

        public int SaveChangesCount { get; private set; }

        public Task<IReadOnlyList<OutboxMessage>> GetDueAsync(
            DateTimeOffset dueAtUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<OutboxMessage>>(_messages
                .Where(message =>
                    (message.Status == OutboxMessageStatus.Pending || message.Status == OutboxMessageStatus.Failed)
                    && message.NextAttemptAtUtc is not null
                    && message.NextAttemptAtUtc <= dueAtUtc)
                .Take(batchSize)
                .ToList());
        }

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            _messages.Add(message);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeApiClientRepository : IApiClientRepository
    {
        private readonly List<ApiClient> _apiClients;

        public FakeApiClientRepository(IReadOnlyList<ApiClient> apiClients)
        {
            _apiClients = apiClients.ToList();
        }

        public IReadOnlyList<ApiClient> ApiClients => _apiClients.AsReadOnly();

        public int SaveChangesCount { get; private set; }

        public Task<ApiClient?> GetByIdAsync(
            Guid apiClientId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apiClients.FirstOrDefault(apiClient => apiClient.Id == apiClientId));
        }

        public Task<ApiClient?> GetByApiKeyHashAsync(
            string apiKeyHash,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apiClients.FirstOrDefault(apiClient => apiClient.ApiKeyHash == apiKeyHash));
        }

        public Task<IReadOnlyList<ApiClient>> GetByShopIdsAsync(
            IReadOnlyCollection<Guid> shopIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApiClient>>(_apiClients
                .Where(apiClient => shopIds.Contains(apiClient.ShopId))
                .ToList());
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

        public Task<ExternalShipmentReference?> GetByShipmentIdAsync(
            Guid shipmentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_references.FirstOrDefault(reference =>
                reference.ShipmentId == shipmentId));
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

        public Task<IReadOnlyList<Shop>> GetAllByOwnerUserIdAsync(
            Guid ownerUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shop>>(_shops
                .Where(shop => shop.OwnerUserId == ownerUserId)
                .ToList());
        }

        public Task<IReadOnlyList<Shop>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Shop>>(_shops.ToList());
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

        public Task<IReadOnlyDictionary<Guid, int>> GetActiveAssignmentCountsByShipperIdsAsync(
            IReadOnlyCollection<Guid> shipperIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, int> counts = _shipments
                .Where(shipment => ShipmentLoadStatuses.ActiveAssignmentStatuses.Contains(shipment.Status))
                .SelectMany(shipment => shipment.Assignments.Where(assignment =>
                    assignment.IsActive && shipperIds.Contains(assignment.ShipperId)))
                .GroupBy(assignment => assignment.ShipperId)
                .ToDictionary(group => group.Key, group => group.Count());

            return Task.FromResult(counts);
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

    private sealed class FakeWebhookEndpointRepository : IWebhookEndpointRepository
    {
        private readonly List<WebhookEndpoint> _endpoints;

        public FakeWebhookEndpointRepository(IReadOnlyList<WebhookEndpoint> endpoints)
        {
            _endpoints = endpoints.ToList();
        }

        public IReadOnlyList<WebhookEndpoint> Endpoints => _endpoints.AsReadOnly();

        public Task<IReadOnlyList<WebhookEndpoint>> GetActiveByApiClientIdAsync(
            Guid apiClientId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookEndpoint>>(_endpoints
                .Where(endpoint => endpoint.ApiClientId == apiClientId && endpoint.IsActive)
                .ToList());
        }

        public Task<IReadOnlyList<WebhookEndpoint>> GetByApiClientIdsAsync(
            IReadOnlyCollection<Guid> apiClientIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookEndpoint>>(_endpoints
                .Where(endpoint => apiClientIds.Contains(endpoint.ApiClientId))
                .ToList());
        }

        public Task<WebhookEndpoint?> GetByIdAsync(
            Guid endpointId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_endpoints.FirstOrDefault(endpoint => endpoint.Id == endpointId));
        }

        public Task<WebhookEndpoint?> GetLatestByApiClientIdAsync(
            Guid apiClientId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_endpoints
                .Where(endpoint => endpoint.ApiClientId == apiClientId)
                .OrderByDescending(endpoint => endpoint.IsActive)
                .ThenByDescending(endpoint => endpoint.UpdatedAtUtc ?? endpoint.CreatedAtUtc)
                .FirstOrDefault());
        }

        public Task AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            _endpoints.Add(endpoint);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebhookDeliveryRepository : IWebhookDeliveryRepository
    {
        private readonly List<WebhookDelivery> _deliveries;

        public FakeWebhookDeliveryRepository(IReadOnlyList<WebhookDelivery> deliveries)
        {
            _deliveries = deliveries.ToList();
        }

        public int SaveChangesCount { get; private set; }

        public IReadOnlyList<WebhookDelivery> Deliveries => _deliveries.AsReadOnly();

        public Task<bool> ExistsAsync(
            Guid deliveryId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_deliveries.Any(delivery => delivery.Id == deliveryId));
        }

        public Task<IReadOnlyList<WebhookDelivery>> GetDueAsync(
            DateTimeOffset dueAtUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookDelivery>>(_deliveries
                .Where(delivery =>
                    delivery.Status != WebhookDeliveryStatus.Succeeded
                    && delivery.NextAttemptAtUtc is not null
                    && delivery.NextAttemptAtUtc <= dueAtUtc)
                .Take(batchSize)
                .ToList());
        }

        public Task<IReadOnlyList<WebhookDelivery>> GetRecentByApiClientIdsAsync(
            IReadOnlyCollection<Guid> apiClientIds,
            int takePerClient,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookDelivery>>(_deliveries
                .Where(delivery => apiClientIds.Contains(delivery.ApiClientId))
                .GroupBy(delivery => delivery.ApiClientId)
                .SelectMany(group => group
                    .OrderByDescending(delivery => delivery.CreatedAtUtc)
                    .Take(takePerClient))
                .ToList());
        }

        public Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
        {
            _deliveries.Add(delivery);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }
}
