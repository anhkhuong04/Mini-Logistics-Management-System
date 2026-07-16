using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shipments.BulkRetryAutoAssignment;

public sealed class BulkRetryAutoAssignmentService : IBulkRetryAutoAssignmentService
{
    private readonly IValidator<BulkRetryAutoAssignmentCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IAutoAssignShipmentService _autoAssignShipmentService;

    public BulkRetryAutoAssignmentService(
        IValidator<BulkRetryAutoAssignmentCommand> validator,
        IIdentityService identityService,
        IShipmentRepository shipmentRepository,
        IAutoAssignShipmentService autoAssignShipmentService)
    {
        _validator = validator;
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _autoAssignShipmentService = autoAssignShipmentService;
    }

    public async Task<Result<BulkRetryAutoAssignmentResult>> RetryAsync(
        BulkRetryAutoAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<BulkRetryAutoAssignmentResult>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var authorizationResult = await ValidateActorAsync(command.RequestedByUserId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<BulkRetryAutoAssignmentResult>.Failure(authorizationResult.Error);
        }

        var requestedIds = command.ShipmentIds.ToHashSet();
        var shipments = await _shipmentRepository.GetByIdsAsync(requestedIds, cancellationToken);
        var shipmentById = shipments.ToDictionary(shipment => shipment.Id);
        var items = new List<BulkRetryAutoAssignmentItem>();
        var retriedCount = 0;
        var assignedCount = 0;

        foreach (var shipmentId in command.ShipmentIds)
        {
            if (!shipmentById.TryGetValue(shipmentId, out var shipment))
            {
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
        return Result<BulkRetryAutoAssignmentResult>.Success(new BulkRetryAutoAssignmentResult(
            command.ShipmentIds.Count,
            retriedCount,
            assignedCount,
            skippedCount,
            items));
    }

    private async Task<Result> ValidateActorAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var adminCheck = await _identityService.CheckUserRoleAsync(
            actorUserId,
            nameof(UserRole.Admin),
            cancellationToken);

        if (!adminCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Bulk retry user was not found."));
        }

        if (!adminCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Bulk retry user is not active."));
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
            : Result.Failure(ApplicationErrors.Forbidden("Only Admin or Operator can bulk retry assignment."));
    }
}
