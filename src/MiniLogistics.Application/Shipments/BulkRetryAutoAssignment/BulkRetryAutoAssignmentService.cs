using FluentValidation;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.BulkRetryAutoAssignment;

public sealed class BulkRetryAutoAssignmentService : IBulkRetryAutoAssignmentService
{
    private readonly IValidator<BulkRetryAutoAssignmentCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentReadRepository _shipmentRepository;
    private readonly IAutoAssignShipmentService _autoAssignShipmentService;
    private readonly IOperationAuthorizationService _operationAuthorizationService;
    private readonly IAdminAuditService _adminAuditService;
    private readonly ILogger<BulkRetryAutoAssignmentService>? _logger;

    public BulkRetryAutoAssignmentService(
        IValidator<BulkRetryAutoAssignmentCommand> validator,
        IIdentityService identityService,
        IShipmentReadRepository shipmentRepository,
        IAutoAssignShipmentService autoAssignShipmentService,
        ILogger<BulkRetryAutoAssignmentService>? logger = null,
        IOperationAuthorizationService? operationAuthorizationService = null,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _autoAssignShipmentService = autoAssignShipmentService;
        _operationAuthorizationService = operationAuthorizationService ?? new OperationAuthorizationService(identityService);
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _logger = logger;
    }

    public async Task<Result<BulkRetryAutoAssignmentResult>> RetryAsync(
        BulkRetryAutoAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            _logger?.LogWarning(
                "Bulk retry auto-assignment validation failed for requester {RequestedByUserId}: {ValidationErrors}",
                command.RequestedByUserId,
                description);
            return Result<BulkRetryAutoAssignmentResult>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var authorizationResult = await ValidateActorAsync(command.RequestedByUserId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            _logger?.LogWarning(
                "Bulk retry auto-assignment authorization failed for requester {RequestedByUserId} with error {ErrorCode}",
                command.RequestedByUserId,
                authorizationResult.Error.Code);
            return Result<BulkRetryAutoAssignmentResult>.Failure(authorizationResult.Error);
        }

        var requestedIds = command.ShipmentIds.ToHashSet();
        _logger?.LogInformation(
            "Bulk retry auto-assignment started by {RequestedByUserId} for {ShipmentCount} shipments",
            command.RequestedByUserId,
            requestedIds.Count);
        var shipments = await _shipmentRepository.GetByIdsAsync(requestedIds, cancellationToken);
        var shipmentById = shipments.ToDictionary(shipment => shipment.Id);
        var items = new List<BulkRetryAutoAssignmentItem>();
        var retriedCount = 0;
        var assignedCount = 0;

        foreach (var shipmentId in command.ShipmentIds)
        {
            if (!shipmentById.TryGetValue(shipmentId, out var shipment))
            {
                _logger?.LogWarning(
                    "Bulk retry auto-assignment skipped missing shipment {ShipmentId}",
                    shipmentId);
                items.Add(new BulkRetryAutoAssignmentItem(
                    shipmentId,
                    string.Empty,
                    "NotFound",
                    "Skipped",
                    "Shipment was not found."));
                continue;
            }

            if (shipment.Status != ShipmentStatus.PendingPickup)
            {
                _logger?.LogInformation(
                    "Bulk retry auto-assignment skipped shipment {ShipmentId} because status is {ShipmentStatus}",
                    shipment.Id,
                    shipment.Status);
                items.Add(new BulkRetryAutoAssignmentItem(
                    shipment.Id,
                    shipment.TrackingCode.Value,
                    shipment.Status.ToString(),
                    "Skipped",
                    "Only PendingPickup shipments can be retried."));
                continue;
            }

            retriedCount++;
            var autoAssignResult = await _autoAssignShipmentService.AutoAssignAsync(
                shipment.Id,
                cancellationToken,
                command.RequestedByUserId);
            if (autoAssignResult.IsFailure)
            {
                _logger?.LogWarning(
                    "Bulk retry auto-assignment failed for shipment {ShipmentId} with error {ErrorCode}: {ErrorDescription}",
                    shipment.Id,
                    autoAssignResult.Error.Code,
                    autoAssignResult.Error.Description);
                items.Add(new BulkRetryAutoAssignmentItem(
                    shipment.Id,
                    shipment.TrackingCode.Value,
                    shipment.Status.ToString(),
                    "Failed",
                    autoAssignResult.Error.Description));
                continue;
            }

            if (autoAssignResult.Value.Status == AutoAssignShipmentStatus.Assigned)
            {
                assignedCount++;
            }

            items.Add(new BulkRetryAutoAssignmentItem(
                shipment.Id,
                autoAssignResult.Value.TrackingCode,
                autoAssignResult.Value.ShipmentStatus.ToString(),
                autoAssignResult.Value.Status.ToString(),
                autoAssignResult.Value.Reason));
        }

        var skippedCount = items.Count(item => item.Result == "Skipped");
        _logger?.LogInformation(
            "Bulk retry auto-assignment completed by {RequestedByUserId}: requested {RequestedCount}, retried {RetriedCount}, assigned {AssignedCount}, skipped {SkippedCount}",
            command.RequestedByUserId,
            command.ShipmentIds.Count,
            retriedCount,
            assignedCount,
            skippedCount);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.RequestedByUserId,
                AdminAuditActions.ShipmentBulkAutoAssignmentRetried,
                AdminAuditTargetTypes.Shipment,
                command.ShipmentIds.First(),
                NewValue: new
                {
                    RequestedCount = command.ShipmentIds.Count,
                    retriedCount,
                    assignedCount,
                    skippedCount
                }),
            cancellationToken);
        await _adminAuditService.SaveChangesAsync(cancellationToken);

        return Result<BulkRetryAutoAssignmentResult>.Success(new BulkRetryAutoAssignmentResult(
            command.ShipmentIds.Count,
            retriedCount,
            assignedCount,
            skippedCount,
            items));
    }

    private async Task<Result> ValidateActorAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        return await _operationAuthorizationService.EnsurePermissionAsync(
            actorUserId,
            OperationPermissions.AssignmentBulkRetryAuto,
            "Bulk retry user was not found.",
            "Bulk retry user is not active.",
            "Only Admin or Operator can bulk retry assignment.",
            cancellationToken);
    }
}
