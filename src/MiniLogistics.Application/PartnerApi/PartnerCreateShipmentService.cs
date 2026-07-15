using System.Text.Json;
using FluentValidation;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerCreateShipmentService : IPartnerCreateShipmentService
{
    private const int MaxTrackingCodeGenerationAttempts = 5;

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IValidator<PartnerCreateShipmentCommand> _validator;
    private readonly IShopRepository _shopRepository;
    private readonly IRouteClassificationService _routeClassificationService;
    private readonly IShippingFeeService _shippingFeeService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly IExternalShipmentReferenceRepository _externalShipmentReferenceRepository;
    private readonly IAutoAssignShipmentService _autoAssignShipmentService;
    private readonly IWebhookEventPublisher _webhookEventPublisher;

    public PartnerCreateShipmentService(
        IValidator<PartnerCreateShipmentCommand> validator,
        IShopRepository shopRepository,
        IRouteClassificationService routeClassificationService,
        IShippingFeeService shippingFeeService,
        IShipmentRepository shipmentRepository,
        ICodTransactionRepository codTransactionRepository,
        IExternalShipmentReferenceRepository externalShipmentReferenceRepository,
        IAutoAssignShipmentService autoAssignShipmentService,
        IWebhookEventPublisher? webhookEventPublisher = null)
    {
        _validator = validator;
        _shopRepository = shopRepository;
        _routeClassificationService = routeClassificationService;
        _shippingFeeService = shippingFeeService;
        _shipmentRepository = shipmentRepository;
        _codTransactionRepository = codTransactionRepository;
        _externalShipmentReferenceRepository = externalShipmentReferenceRepository;
        _autoAssignShipmentService = autoAssignShipmentService;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
    }

    public async Task<Result<PartnerCreateShipmentResult>> CreateAsync(
        PartnerCreateShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<PartnerCreateShipmentResult>.Failure(ToValidationError(validationResult.Errors));
        }

        var requestHash = CreateRequestHash(command);
        var idempotentReference = await _externalShipmentReferenceRepository.GetByApiClientAndIdempotencyKeyAsync(
            command.ApiClientId,
            command.IdempotencyKey,
            cancellationToken);
        if (idempotentReference is not null)
        {
            if (!string.Equals(idempotentReference.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return Result<PartnerCreateShipmentResult>.Failure(PartnerApiErrors.IdempotencyConflict);
            }

            var snapshot = JsonSerializer.Deserialize<PartnerShipmentResponse>(
                idempotentReference.ResponseSnapshotJson,
                SnapshotJsonOptions);
            if (snapshot is null)
            {
                return Result<PartnerCreateShipmentResult>.Failure(ApplicationErrors.Conflict("Stored idempotent response is invalid."));
            }

            return Result<PartnerCreateShipmentResult>.Success(new PartnerCreateShipmentResult(snapshot, true));
        }

        var existingExternalOrder = await _externalShipmentReferenceRepository.GetByApiClientAndExternalOrderIdAsync(
            command.ApiClientId,
            command.ExternalOrderId,
            cancellationToken);
        if (existingExternalOrder is not null)
        {
            return Result<PartnerCreateShipmentResult>.Failure(ApplicationErrors.Conflict("External order already has a shipment."));
        }

        var shop = await _shopRepository.GetByIdAsync(command.ShopId, cancellationToken);
        if (shop is null)
        {
            return Result<PartnerCreateShipmentResult>.Failure(ApplicationErrors.NotFound("Shop was not found for API client."));
        }

        if (!shop.IsActive)
        {
            return Result<PartnerCreateShipmentResult>.Failure(ApplicationErrors.Forbidden("Shop account is not active."));
        }

        var senderName = string.IsNullOrWhiteSpace(command.SenderName)
            ? shop.Name
            : command.SenderName.Trim();
        var senderPhone = string.IsNullOrWhiteSpace(command.SenderPhone)
            ? shop.PhoneNumber.Value
            : command.SenderPhone.Trim();
        var pickupAddress = command.PickupAddress ?? ToDto(shop.Address);

        var routeResult = _routeClassificationService.Classify(
            pickupAddress.Province,
            command.DeliveryAddress.Province);
        if (routeResult.IsFailure)
        {
            return Result<PartnerCreateShipmentResult>.Failure(routeResult.Error);
        }

        var weight = new Weight(command.WeightKg);
        var parcelDimensions = new ParcelDimensions(command.LengthCm, command.WidthCm, command.HeightCm);
        var goodsValue = new Money(command.GoodsValueAmount, command.Currency);
        var codAmount = new Money(command.CodAmount, command.Currency);
        var feeResult = await _shippingFeeService.CalculateAsync(
            routeResult.Value.RouteType,
            weight,
            parcelDimensions,
            goodsValue,
            cancellationToken);
        if (feeResult.IsFailure)
        {
            return Result<PartnerCreateShipmentResult>.Failure(feeResult.Error);
        }

        var trackingCode = await GenerateUniqueTrackingCodeAsync(cancellationToken);
        var shipment = Shipment.Create(
            shop.Id,
            senderName,
            new PhoneNumber(senderPhone),
            command.ReceiverName,
            new PhoneNumber(command.ReceiverPhone),
            ToAddress(pickupAddress),
            ToAddress(command.DeliveryAddress),
            weight,
            parcelDimensions,
            new Weight(feeResult.Value.ChargeableWeightKg),
            goodsValue,
            codAmount,
            feeResult.Value.Breakdown,
            routeResult.Value.RouteType,
            shop.OwnerUserId,
            command.Note,
            trackingCode);
        var codTransaction = CodTransaction.Create(shipment.Id, codAmount);

        var response = ToResponse(shipment, command.ExternalOrderId);
        var responseJson = JsonSerializer.Serialize(response, SnapshotJsonOptions);
        var reference = new ExternalShipmentReference(
            command.ApiClientId,
            shop.Id,
            shipment.Id,
            command.ExternalOrderId,
            command.IdempotencyKey,
            requestHash,
            responseJson);

        await _shipmentRepository.AddAsync(shipment, cancellationToken);
        await _codTransactionRepository.AddAsync(codTransaction, cancellationToken);
        await _externalShipmentReferenceRepository.AddAsync(reference, cancellationToken);
        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            reference,
            WebhookEventTypes.ShipmentCreated,
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        await _autoAssignShipmentService.AutoAssignAsync(shipment.Id, cancellationToken);

        var finalResponse = ToResponse(shipment, command.ExternalOrderId);
        if (finalResponse.Status != response.Status)
        {
            reference.UpdateResponseSnapshot(JsonSerializer.Serialize(finalResponse, SnapshotJsonOptions));
            await _shipmentRepository.SaveChangesAsync(cancellationToken);
        }

        return Result<PartnerCreateShipmentResult>.Success(new PartnerCreateShipmentResult(finalResponse, false));
    }

    private async Task<TrackingCode> GenerateUniqueTrackingCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxTrackingCodeGenerationAttempts; attempt++)
        {
            var trackingCode = TrackingCode.Generate();
            var exists = await _shipmentRepository.ExistsByTrackingCodeAsync(trackingCode, cancellationToken);

            if (!exists)
            {
                return trackingCode;
            }
        }

        throw new InvalidOperationException("Could not generate a unique tracking code.");
    }

    private static string CreateRequestHash(PartnerCreateShipmentCommand command)
    {
        var fingerprint = new PartnerCreateShipmentFingerprint(
            command.ExternalOrderId.Trim(),
            command.SenderName?.Trim(),
            command.SenderPhone?.Trim(),
            command.ReceiverName.Trim(),
            command.ReceiverPhone.Trim(),
            command.PickupAddress,
            command.DeliveryAddress,
            command.WeightKg,
            command.LengthCm,
            command.WidthCm,
            command.HeightCm,
            command.GoodsValueAmount,
            command.CodAmount,
            command.Currency.Trim().ToUpperInvariant(),
            command.Note?.Trim());
        var payload = JsonSerializer.Serialize(fingerprint, SnapshotJsonOptions);

        return ApiKeyHasher.Hash(payload);
    }

    private static ShipmentAddressDto ToDto(Address address)
    {
        return new ShipmentAddressDto(
            address.Street,
            address.Ward,
            address.Province,
            address.Country);
    }

    private static Address ToAddress(ShipmentAddressDto address)
    {
        return new Address(
            address.Street,
            address.Ward,
            address.Province,
            address.Country);
    }

    private static PartnerShipmentResponse ToResponse(
        Shipment shipment,
        string externalOrderId)
    {
        return new PartnerShipmentResponse(
            shipment.Id,
            externalOrderId,
            shipment.TrackingCode.Value,
            shipment.Status,
            shipment.RouteType,
            shipment.ShippingFee.Amount,
            shipment.ShippingFee.Currency,
            shipment.CreatedAtUtc);
    }

    private static Error ToValidationError(IEnumerable<FluentValidation.Results.ValidationFailure> errors)
    {
        return ApplicationErrors.ValidationFailed(string.Join("; ", errors.Select(error => error.ErrorMessage)));
    }

    private sealed record PartnerCreateShipmentFingerprint(
        string ExternalOrderId,
        string? SenderName,
        string? SenderPhone,
        string ReceiverName,
        string ReceiverPhone,
        ShipmentAddressDto? PickupAddress,
        ShipmentAddressDto DeliveryAddress,
        decimal WeightKg,
        decimal LengthCm,
        decimal WidthCm,
        decimal HeightCm,
        decimal GoodsValueAmount,
        decimal CodAmount,
        string Currency,
        string? Note);
}
