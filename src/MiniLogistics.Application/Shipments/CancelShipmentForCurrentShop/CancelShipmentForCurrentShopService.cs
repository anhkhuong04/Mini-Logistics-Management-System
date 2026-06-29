using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shops;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;

public sealed class CancelShipmentForCurrentShopService : ICancelShipmentForCurrentShopService
{
    private readonly IValidator<CancelShipmentCommand> _validator;
    private readonly IShopRepository _shopRepository;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IWebhookEventPublisher _webhookEventPublisher;

    public CancelShipmentForCurrentShopService(
        IValidator<CancelShipmentCommand> validator,
        IShopRepository shopRepository,
        IShipmentRepository shipmentRepository,
        IWebhookEventPublisher? webhookEventPublisher = null)
    {
        _validator = validator;
        _shopRepository = shopRepository;
        _shipmentRepository = shipmentRepository;
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

        var shop = await _shopRepository.GetByOwnerUserIdAsync(command.OwnerUserId, cancellationToken);
        if (shop is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shop was not found for current user."));
        }

        if (!shop.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Shop account is not active."));
        }

        var shipment = await _shipmentRepository.GetTrackedByIdAndShopIdAsync(
            command.ShipmentId,
            shop.Id,
            cancellationToken);

        if (shipment is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipment was not found for current shop."));
        }

        var cancelResult = shipment.Cancel(command.OwnerUserId, command.Reason);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        await _shipmentRepository.SaveChangesAsync(cancellationToken);
        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);

        return Result.Success();
    }
}
