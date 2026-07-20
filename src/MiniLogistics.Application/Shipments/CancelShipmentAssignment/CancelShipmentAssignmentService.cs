using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.CancelShipmentAssignment;

public sealed class CancelShipmentAssignmentService : ICancelShipmentAssignmentService
{
    private readonly IValidator<CancelShipmentAssignmentCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IWebhookEventPublisher _webhookEventPublisher;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IOperationAuthorizationService _operationAuthorizationService;
    private readonly TimeProvider _timeProvider;

    public CancelShipmentAssignmentService(
        IValidator<CancelShipmentAssignmentCommand> validator,
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

    public async Task<Result> CancelAsync(
        CancelShipmentAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var actorResult = await ValidateActorAsync(command.CancelledByUserId, cancellationToken);
        if (actorResult.IsFailure)
        {
            return actorResult;
        }

        var shipment = await _shipmentRepository.GetTrackedByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipment was not found."));
        }

        var previousShipperId = shipment.Assignments
            .FirstOrDefault(assignment => assignment.IsActive)
            ?.ShipperId;
        var previousStatus = shipment.Status;

        var cancelResult = shipment.CancelActiveAssignment(
            command.CancelledByUserId,
            _timeProvider.GetUtcNow(),
            command.Reason);
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
                command.CancelledByUserId,
                AdminAuditActions.ShipmentAssignmentCancelled,
                AdminAuditTargetTypes.Shipment,
                shipment.Id,
                OldValue: new
                {
                    Status = previousStatus.ToString(),
                    AssignedShipperId = previousShipperId
                },
                NewValue: new
                {
                    Status = shipment.Status.ToString(),
                    AssignedShipperId = (Guid?)null
                },
                Reason: command.Reason),
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateActorAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        return await _operationAuthorizationService.EnsurePermissionAsync(
            actorUserId,
            OperationPermissions.AssignmentCancel,
            "Assignment cancellation user was not found.",
            "Assignment cancellation user is not active.",
            "Only Admin or Operator can cancel shipment assignments.",
            cancellationToken);
    }
}
