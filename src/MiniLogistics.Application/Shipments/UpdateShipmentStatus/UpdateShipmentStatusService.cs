using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shipments.UpdateShipmentStatus;

public sealed class UpdateShipmentStatusService : IUpdateShipmentStatusService
{
    private readonly IValidator<UpdateShipmentStatusCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IWebhookEventPublisher _webhookEventPublisher;

    public UpdateShipmentStatusService(
        IValidator<UpdateShipmentStatusCommand> validator,
        IIdentityService identityService,
        IShipmentRepository shipmentRepository,
        IWebhookEventPublisher? webhookEventPublisher = null)
    {
        _validator = validator;
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
    }

    public async Task<Result> UpdateAsync(
        UpdateShipmentStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var shipment = await _shipmentRepository.GetTrackedByIdAsync(
            command.ShipmentId,
            cancellationToken);

        if (shipment is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipment was not found."));
        }

        var authorizationResult = await ValidateUpdatePermissionAsync(
            command.ChangedByUserId,
            shipment,
            cancellationToken);

        if (authorizationResult.IsFailure)
        {
            return authorizationResult;
        }

        var updateResult = shipment.UpdateStatus(
            command.NewStatus,
            command.ChangedByUserId,
            command.Note);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await _shipmentRepository.SaveChangesAsync(cancellationToken);
        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateUpdatePermissionAsync(
        Guid changedByUserId,
        Shipment shipment,
        CancellationToken cancellationToken)
    {
        var adminCheck = await _identityService.CheckUserRoleAsync(
            changedByUserId,
            nameof(UserRole.Admin),
            cancellationToken);

        if (!adminCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Status update user was not found."));
        }

        if (!adminCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Status update user is not active."));
        }

        if (adminCheck.IsInRole)
        {
            return Result.Success();
        }

        var operatorCheck = await _identityService.CheckUserRoleAsync(
            changedByUserId,
            nameof(UserRole.Operator),
            cancellationToken);

        if (operatorCheck.IsInRole)
        {
            return Result.Success();
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            changedByUserId,
            nameof(UserRole.Shipper),
            cancellationToken);

        if (!shipperCheck.IsInRole)
        {
            return Result.Failure(ApplicationErrors.Forbidden(
                "Only Admin, Operator or assigned Shipper can update shipment status."));
        }

        var hasActiveAssignment = shipment.Assignments.Any(assignment =>
            assignment.IsActive && assignment.ShipperId == changedByUserId);

        return hasActiveAssignment
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden(
                "Shipper can only update shipments assigned to them."));
    }
}
