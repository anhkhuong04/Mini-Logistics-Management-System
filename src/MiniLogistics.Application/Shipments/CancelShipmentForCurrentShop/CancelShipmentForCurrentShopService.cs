using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
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
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;

    public CancelShipmentForCurrentShopService(
        IValidator<CancelShipmentCommand> validator,
        IShopAccessService shopAccessService,
        IShipmentRepository shipmentRepository,
        TimeProvider timeProvider,
        IWebhookEventPublisher? webhookEventPublisher = null,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
        _timeProvider = timeProvider;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
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

        var previousStatus = shipment.Status;
        var cancelResult = shipment.Cancel(command.OwnerUserId, _timeProvider.GetUtcNow(), command.Reason);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.OwnerUserId,
                AdminAuditActions.ShipmentCancelledByShop,
                AdminAuditTargetTypes.Shipment,
                shipment.Id,
                OldValue: new
                {
                    Status = previousStatus.ToString()
                },
                NewValue: new
                {
                    Status = shipment.Status.ToString()
                },
                Reason: command.Reason),
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
