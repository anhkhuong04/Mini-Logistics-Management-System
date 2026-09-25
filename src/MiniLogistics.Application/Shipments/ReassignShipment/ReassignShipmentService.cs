using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shops.Notifications;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shipments.ReassignShipment;

public sealed class ReassignShipmentService : IReassignShipmentService
{
    private readonly IValidator<ReassignShipmentCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IShipperAssignmentCapacityGuard _assignmentCapacityGuard;
    private readonly IWebhookEventPublisher _webhookEventPublisher;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IOperationAuthorizationService _operationAuthorizationService;
    private readonly TimeProvider _timeProvider;
    private readonly IShopNotificationService _shopNotificationService;

    public ReassignShipmentService(
        IValidator<ReassignShipmentCommand> validator,
        IIdentityService identityService,
        IShipmentRepository shipmentRepository,
        TimeProvider timeProvider,
        IShipperAssignmentCapacityGuard assignmentCapacityGuard,
        IWebhookEventPublisher? webhookEventPublisher = null,
        IAdminAuditService? adminAuditService = null,
        IOperationAuthorizationService? operationAuthorizationService = null,
        IShopNotificationService? shopNotificationService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _assignmentCapacityGuard = assignmentCapacityGuard;
        _timeProvider = timeProvider;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _operationAuthorizationService = operationAuthorizationService ?? new OperationAuthorizationService(identityService);
        _shopNotificationService = shopNotificationService ?? NullShopNotificationService.Instance;
    }

    public async Task<Result> ReassignAsync(
        ReassignShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var actorResult = await ValidateActorAsync(command.ReassignedByUserId, cancellationToken);
        if (actorResult.IsFailure)
        {
            return actorResult;
        }

        var shipperResult = await ValidateShipperAsync(command.NewShipperId, cancellationToken);
        if (shipperResult.IsFailure)
        {
            return shipperResult;
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

        IShipperAssignmentCapacityLease? capacityLease = null;
        if (previousShipperId != command.NewShipperId)
        {
            capacityLease = await _assignmentCapacityGuard.TryAcquireAsync(
                command.NewShipperId,
                cancellationToken);
            if (capacityLease is null)
            {
                return Result.Failure(ApplicationErrors.CapacityReached(
                    "Selected shipper has reached the maximum number of active shipments."));
            }
        }

        await using var capacityLeaseScope = capacityLease;

        var reassignResult = shipment.ReassignShipper(
            command.NewShipperId,
            command.ReassignedByUserId,
            _timeProvider.GetUtcNow(),
            command.Reason);
        if (reassignResult.IsFailure)
        {
            return reassignResult;
        }

        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);
        await _shopNotificationService.QueueShipmentEventAsync(
            shipment,
            ShopNotificationEventTypes.Assigned,
            cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.ReassignedByUserId,
                AdminAuditActions.ShipmentReassigned,
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
                    AssignedShipperId = command.NewShipperId
                },
                Reason: command.Reason),
            cancellationToken);
        await _shipmentRepository.SaveChangesAsync(cancellationToken);
        if (capacityLease is not null)
        {
            await capacityLease.CommitAsync(cancellationToken);
        }

        return Result.Success();
    }

    private async Task<Result> ValidateActorAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        return await _operationAuthorizationService.EnsurePermissionAsync(
            actorUserId,
            OperationPermissions.AssignmentReassign,
            "Reassigning user was not found.",
            "Reassigning user is not active.",
            "Only Admin or Operator can reassign shipments.",
            cancellationToken);
    }

    private async Task<Result> ValidateShipperAsync(Guid shipperId, CancellationToken cancellationToken)
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
