using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Authorization;
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
    private readonly IAdminAuditService _adminAuditService;
    private readonly IOperationAuthorizationService _operationAuthorizationService;
    private readonly TimeProvider _timeProvider;

    public AssignShipperToShipmentService(
        IValidator<AssignShipperCommand> validator,
        IIdentityService identityService,
        IShipmentRepository shipmentRepository,
        TimeProvider timeProvider,
        IWebhookEventPublisher? webhookEventPublisher = null,
        IAdminAuditService? adminAuditService = null,
        IOperationAuthorizationService? operationAuthorizationService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _timeProvider = timeProvider;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _operationAuthorizationService = operationAuthorizationService ?? new OperationAuthorizationService(identityService);
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
            _timeProvider.GetUtcNow(),
            command.Note);

        if (assignResult.IsFailure)
        {
            return assignResult;
        }

        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.AssignedByUserId,
                AdminAuditActions.ShipmentManualAssigned,
                AdminAuditTargetTypes.Shipment,
                shipment.Id,
                OldValue: new
                {
                    Status = "PendingPickup",
                    AssignedShipperId = (Guid?)null
                },
                NewValue: new
                {
                    Status = shipment.Status.ToString(),
                    AssignedShipperId = command.ShipperId
                },
                Reason: command.Note),
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateAssignedByUserAsync(
        Guid assignedByUserId,
        CancellationToken cancellationToken)
    {
        return await _operationAuthorizationService.EnsurePermissionAsync(
            assignedByUserId,
            OperationPermissions.AssignmentAssign,
            "Assigning user was not found.",
            "Assigning user is not active.",
            "Only Admin or Operator can assign shippers.",
            cancellationToken);
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
