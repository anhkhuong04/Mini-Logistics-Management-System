using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;

public sealed class CancelShipmentForCurrentShopService : ICancelShipmentForCurrentShopService
{
    private readonly IValidator<CancelShipmentCommand> _validator;
    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IWebhookEventPublisher _webhookEventPublisher;
    private readonly TimeProvider _timeProvider;

    public CancelShipmentForCurrentShopService(
        IValidator<CancelShipmentCommand> validator,
        IShopAccessService shopAccessService,
        IShipmentRepository shipmentRepository,
        TimeProvider timeProvider,
        IWebhookEventPublisher? webhookEventPublisher = null)
    {
        _validator = validator;
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
        _timeProvider = timeProvider;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
    }

    public async Task<Result> CancelAsync(
        CancelShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var shopResult = await _shopAccessService.GetShopForUserAsync(
            command.OwnerUserId,
            command.ShopId,
            requireActiveShop: true,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result.Failure(shopResult.Error);
        }

        var shop = shopResult.Value;
        var shipment = await _shipmentRepository.GetTrackedByIdAndShopIdAsync(
            command.ShipmentId,
            shop.Id,
            cancellationToken);

        if (shipment is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipment was not found for current shop."));
        }

        var cancelResult = shipment.Cancel(command.OwnerUserId, _timeProvider.GetUtcNow(), command.Reason);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
