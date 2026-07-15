using FluentValidation;
using MiniLogistics.Application.CashOnDelivery;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerCancelShipmentService : IPartnerCancelShipmentService
{
    private readonly IValidator<PartnerCancelShipmentCommand> _validator;
    private readonly IShopRepository _shopRepository;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly IExternalShipmentReferenceRepository _externalShipmentReferenceRepository;
    private readonly IWebhookEventPublisher _webhookEventPublisher;

    public PartnerCancelShipmentService(
        IValidator<PartnerCancelShipmentCommand> validator,
        IShopRepository shopRepository,
        IShipmentRepository shipmentRepository,
        ICodTransactionRepository codTransactionRepository,
        IExternalShipmentReferenceRepository externalShipmentReferenceRepository,
        IWebhookEventPublisher? webhookEventPublisher = null)
    {
        _validator = validator;
        _shopRepository = shopRepository;
        _shipmentRepository = shipmentRepository;
        _codTransactionRepository = codTransactionRepository;
        _externalShipmentReferenceRepository = externalShipmentReferenceRepository;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
    }

    public async Task<Result<PartnerShipmentTrackingResponse>> CancelAsync(
        PartnerCancelShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<PartnerShipmentTrackingResponse>.Failure(ToValidationError(validationResult.Errors));
        }

        var shop = await _shopRepository.GetByIdAsync(command.ShopId, cancellationToken);
        if (shop is null)
        {
            return Result<PartnerShipmentTrackingResponse>.Failure(ApplicationErrors.NotFound("Shop was not found for API client."));
        }

        if (!shop.IsActive)
        {
            return Result<PartnerShipmentTrackingResponse>.Failure(ApplicationErrors.Forbidden("Shop account is not active."));
        }

        var shipment = await _shipmentRepository.GetTrackedByTrackingCodeAndShopIdAsync(
            new TrackingCode(command.TrackingCode),
            shop.Id,
            cancellationToken);
        if (shipment is null)
        {
            return Result<PartnerShipmentTrackingResponse>.Failure(ApplicationErrors.NotFound("Shipment was not found for API client."));
        }

        var reference = await _externalShipmentReferenceRepository.GetByApiClientAndShipmentIdAsync(
            command.ApiClientId,
            shipment.Id,
            cancellationToken);
        if (reference is null)
        {
            return Result<PartnerShipmentTrackingResponse>.Failure(ApplicationErrors.NotFound("Shipment was not found for API client."));
        }

        var cancelResult = shipment.Cancel(shop.OwnerUserId, command.Reason);
        if (cancelResult.IsFailure)
        {
            return Result<PartnerShipmentTrackingResponse>.Failure(cancelResult.Error);
        }

        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            reference,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        var codTransaction = await _codTransactionRepository.GetByShipmentIdAsync(shipment.Id, cancellationToken);

        return Result<PartnerShipmentTrackingResponse>.Success(
            PartnerShipmentTrackingMapper.ToResponse(shipment, reference, codTransaction));
    }

    private static Error ToValidationError(IEnumerable<FluentValidation.Results.ValidationFailure> errors)
    {
        return ApplicationErrors.ValidationFailed(string.Join("; ", errors.Select(error => error.ErrorMessage)));
    }
}
