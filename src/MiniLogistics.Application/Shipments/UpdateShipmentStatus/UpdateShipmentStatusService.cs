using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.UpdateShipmentStatus;

public sealed class UpdateShipmentStatusService : IUpdateShipmentStatusService
{
    private readonly IValidator<UpdateShipmentStatusCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IWebhookEventPublisher _webhookEventPublisher;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IOperationAuthorizationService _operationAuthorizationService;
    private readonly TimeProvider _timeProvider;

    public UpdateShipmentStatusService(
        IValidator<UpdateShipmentStatusCommand> validator,
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

        var oldStatus = shipment.Status;
        var updateResult = UpdateShipmentStatus(shipment, command);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);
        var auditAction = await ResolveStatusAuditActionAsync(command.ChangedByUserId, cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.ChangedByUserId,
                auditAction,
                AdminAuditTargetTypes.Shipment,
                shipment.Id,
                OldValue: new
                {
                    Status = oldStatus.ToString()
                },
                NewValue: new
                {
                    Status = shipment.Status.ToString(),
                    FailureReasonCode = command.FailureReasonCode?.ToString(),
                    HasGps = command.GpsCoordinate is not null
                },
                Reason: command.Note),
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private Result UpdateShipmentStatus(
        Shipment shipment,
        UpdateShipmentStatusCommand command)
    {
        try
        {
            var gpsCoordinate = command.GpsCoordinate is null
                ? null
                : new GpsCoordinate(
                    command.GpsCoordinate.Latitude,
                    command.GpsCoordinate.Longitude,
                    command.GpsCoordinate.AccuracyMeters,
                    command.GpsCoordinate.CapturedAtUtc);

            return shipment.UpdateStatus(
                command.NewStatus,
                command.ChangedByUserId,
                _timeProvider.GetUtcNow(),
                command.Note,
                command.FailureReasonCode,
                gpsCoordinate);
        }
        catch (DomainException exception)
        {
            return Result.Failure(ApplicationErrors.ValidationFailed(exception.Message));
        }
    }

    private async Task<string> ResolveStatusAuditActionAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var operatorCheck = await _identityService.CheckUserRoleAsync(
            actorUserId,
            nameof(UserRole.Operator),
            cancellationToken);
        if (operatorCheck.Exists && operatorCheck.IsInRole)
        {
            return AdminAuditActions.ShipmentStatusChangedByOperator;
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            actorUserId,
            nameof(UserRole.Shipper),
            cancellationToken);

        return shipperCheck.Exists && shipperCheck.IsInRole
            ? AdminAuditActions.ShipmentStatusChangedByShipper
            : AdminAuditActions.ShipmentStatusChanged;
    }

    private async Task<Result> ValidateUpdatePermissionAsync(
        Guid changedByUserId,
        Shipment shipment,
        CancellationToken cancellationToken)
    {
        var operationPermission = await _operationAuthorizationService.EnsurePermissionAsync(
            changedByUserId,
            OperationPermissions.ShipmentStatusUpdate,
            "Status update user was not found.",
            "Status update user is not active.",
            "Only Admin, Operator or assigned Shipper can update shipment status.",
            cancellationToken);

        if (operationPermission.IsSuccess)
        {
            return Result.Success();
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            changedByUserId,
            nameof(UserRole.Shipper),
            cancellationToken);

        if (!shipperCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Status update user was not found."));
        }

        if (!shipperCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Status update user is not active."));
        }

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
