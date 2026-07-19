using Microsoft.Extensions.Logging;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.AutoAssignShipment;

public sealed class AutoAssignShipmentService : IAutoAssignShipmentService
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IShipmentAssignmentSelector _assignmentSelector;
    private readonly IWebhookEventPublisher _webhookEventPublisher;
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AutoAssignShipmentService>? _logger;

    public AutoAssignShipmentService(
        IShipmentRepository shipmentRepository,
        IShipmentAssignmentSelector assignmentSelector,
        TimeProvider timeProvider,
        IWebhookEventPublisher? webhookEventPublisher = null,
        IAdminAuditService? adminAuditService = null,
        ILogger<AutoAssignShipmentService>? logger = null)
    {
        _shipmentRepository = shipmentRepository;
        _assignmentSelector = assignmentSelector;
        _timeProvider = timeProvider;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _logger = logger;
    }

    public async Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default,
        Guid? requestedByUserId = null)
    {
        if (shipmentId == Guid.Empty)
        {
            _logger?.LogWarning("Auto-assignment rejected because shipment id is empty");
            return Result<AutoAssignShipmentResult>.Failure(
                ApplicationErrors.ValidationFailed("Shipment id is required."));
        }

        _logger?.LogInformation(
            "Auto-assigning shipment {ShipmentId} requested by {RequestedByUserId}",
            shipmentId,
            requestedByUserId);

        var shipment = await _shipmentRepository.GetTrackedByIdAsync(
            shipmentId,
            cancellationToken);
        if (shipment is null)
        {
            _logger?.LogWarning("Auto-assignment skipped because shipment {ShipmentId} was not found", shipmentId);
            return Result<AutoAssignShipmentResult>.Failure(
                ApplicationErrors.NotFound("Shipment was not found."));
        }

        if (shipment.Status != ShipmentStatus.PendingPickup)
        {
            _logger?.LogInformation(
                "Auto-assignment skipped for shipment {ShipmentId} because status is {ShipmentStatus}",
                shipment.Id,
                shipment.Status);
            return Result<AutoAssignShipmentResult>.Success(
                AutoAssignShipmentResult.Skipped(
                    shipment,
                    $"Shipment status is {shipment.Status}; only PendingPickup shipments can be auto assigned."));
        }

        var selection = await _assignmentSelector.SelectAsync(shipment, cancellationToken);
        if (selection.Status == ShipmentAssignmentSelectionStatus.NoEligibleShipper
            || selection.ShipperId is null)
        {
            _logger?.LogWarning(
                "Auto-assignment found no eligible shipper for shipment {ShipmentId}: {Reason}",
                shipment.Id,
                selection.Reason);
            return Result<AutoAssignShipmentResult>.Success(
                AutoAssignShipmentResult.NoEligibleShipper(shipment, selection.Reason));
        }

        var assignResult = shipment.AssignShipper(
            selection.ShipperId.Value,
            SystemActorIds.AutoAssignment,
            _timeProvider.GetUtcNow(),
            $"Auto assigned. {selection.Reason}");
        if (assignResult.IsFailure)
        {
            _logger?.LogWarning(
                "Auto-assignment failed for shipment {ShipmentId} with error {ErrorCode}: {ErrorDescription}",
                shipment.Id,
                assignResult.Error.Code,
                assignResult.Error.Description);
            return Result<AutoAssignShipmentResult>.Failure(assignResult.Error);
        }

        await _webhookEventPublisher.PublishShipmentAsync(
            shipment,
            WebhookEventTypes.ShipmentStatusChanged,
            cancellationToken);
        if (requestedByUserId.HasValue)
        {
            await _adminAuditService.RecordAsync(
                new AdminAuditEntry(
                    requestedByUserId.Value,
                    AdminAuditActions.ShipmentAutoAssignmentRetried,
                    AdminAuditTargetTypes.Shipment,
                    shipment.Id,
                    OldValue: new
                    {
                        Status = ShipmentStatus.PendingPickup.ToString()
                    },
                    NewValue: new
                    {
                        Status = shipment.Status.ToString(),
                        AssignedShipperId = selection.ShipperId.Value
                    },
                    Reason: selection.Reason),
                cancellationToken);
        }

        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        _logger?.LogInformation(
            "Auto-assigned shipment {ShipmentId} to shipper {ShipperId}",
            shipment.Id,
            selection.ShipperId.Value);

        return Result<AutoAssignShipmentResult>.Success(
            AutoAssignShipmentResult.Assigned(
                shipment,
                selection.ShipperId.Value,
                selection.Reason));
    }
}
