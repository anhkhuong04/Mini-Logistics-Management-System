using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shipments.CancelShipmentAssignment;

public sealed class CancelShipmentAssignmentService : ICancelShipmentAssignmentService
{
    private readonly IValidator<CancelShipmentAssignmentCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IWebhookEventPublisher _webhookEventPublisher;
    private readonly IAdminAuditService _adminAuditService;

    public CancelShipmentAssignmentService(
        IValidator<CancelShipmentAssignmentCommand> validator,
        IIdentityService identityService,
        IShipmentRepository shipmentRepository,
        IWebhookEventPublisher? webhookEventPublisher = null,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
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
        var adminCheck = await _identityService.CheckUserRoleAsync(
            actorUserId,
            nameof(UserRole.Admin),
            cancellationToken);

        if (!adminCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Assignment cancellation user was not found."));
        }

        if (!adminCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Assignment cancellation user is not active."));
        }

        if (adminCheck.IsInRole)
        {
            return Result.Success();
        }

        var operatorCheck = await _identityService.CheckUserRoleAsync(
            actorUserId,
            nameof(UserRole.Operator),
            cancellationToken);

        return operatorCheck.IsInRole
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden("Only Admin or Operator can cancel shipment assignments."));
    }
}
