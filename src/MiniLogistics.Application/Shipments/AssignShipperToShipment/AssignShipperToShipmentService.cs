using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shipments.AssignShipperToShipment;

public sealed class AssignShipperToShipmentService : IAssignShipperToShipmentService
{
    private readonly IValidator<AssignShipperCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IWebhookEventPublisher _webhookEventPublisher;

    public AssignShipperToShipmentService(
        IValidator<AssignShipperCommand> validator,
        IIdentityService identityService,
        IShipmentRepository shipmentRepository,
        IWebhookEventPublisher? webhookEventPublisher = null)
    {
        _validator = validator;
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
    }

    public async Task<Result> AssignAsync(
        AssignShipperCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var authorizationResult = await ValidateAssignedByUserAsync(
            command.AssignedByUserId,
            cancellationToken);

        if (authorizationResult.IsFailure)
        {
            return authorizationResult;
        }

        var shipperValidationResult = await ValidateShipperAsync(
            command.ShipperId,
            cancellationToken);

        if (shipperValidationResult.IsFailure)
        {
            return shipperValidationResult;
        }

        var shipment = await _shipmentRepository.GetTrackedByIdAsync(
            command.ShipmentId,
            cancellationToken);

        if (shipment is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipment was not found."));
        }

        var assignResult = shipment.AssignShipper(
            command.ShipperId,
            command.AssignedByUserId,
            command.Note);

        if (assignResult.IsFailure)
        {
            return assignResult;
        }

        await _shipmentRepository.SaveChangesAsync(cancellationToken);
        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateAssignedByUserAsync(
        Guid assignedByUserId,
        CancellationToken cancellationToken)
    {
        var adminCheck = await _identityService.CheckUserRoleAsync(
            assignedByUserId,
            nameof(UserRole.Admin),
            cancellationToken);

        if (!adminCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Assigning user was not found."));
        }

        if (!adminCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Assigning user is not active."));
        }

        if (adminCheck.IsInRole)
        {
            return Result.Success();
        }

        var operatorCheck = await _identityService.CheckUserRoleAsync(
            assignedByUserId,
            nameof(UserRole.Operator),
            cancellationToken);

        return operatorCheck.IsInRole
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden("Only Admin or Operator can assign shippers."));
    }

    private async Task<Result> ValidateShipperAsync(
        Guid shipperId,
        CancellationToken cancellationToken)
    {
        var shipperCheck = await _identityService.CheckUserRoleAsync(
            shipperId,
            nameof(UserRole.Shipper),
            cancellationToken);

        if (!shipperCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipper was not found."));
        }

        if (!shipperCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Shipper is not active."));
        }

        return shipperCheck.IsInRole
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden("Selected user is not a shipper."));
    }
}
