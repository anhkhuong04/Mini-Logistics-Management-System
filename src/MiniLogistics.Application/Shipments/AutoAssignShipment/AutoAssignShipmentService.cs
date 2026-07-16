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

    public AutoAssignShipmentService(
        IShipmentRepository shipmentRepository,
        IShipmentAssignmentSelector assignmentSelector,
        IWebhookEventPublisher? webhookEventPublisher = null,
        IAdminAuditService? adminAuditService = null)
    {
        _shipmentRepository = shipmentRepository;
        _assignmentSelector = assignmentSelector;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default,
        Guid? requestedByUserId = null)
    {
        if (shipmentId == Guid.Empty)
        {
            return Result<AutoAssignShipmentResult>.Failure(
                ApplicationErrors.ValidationFailed("Shipment id is required."));
        }

        var shipment = await _shipmentRepository.GetTrackedByIdAsync(
            shipmentId,
            cancellationToken);
        if (shipment is null)
        {
            return Result<AutoAssignShipmentResult>.Failure(
                ApplicationErrors.NotFound("Shipment was not found."));
        }

        if (shipment.Status != ShipmentStatus.PendingPickup)
        {
            return Result<AutoAssignShipmentResult>.Success(
                AutoAssignShipmentResult.Skipped(
                    shipment,
                    $"Shipment status is {shipment.Status}; only PendingPickup shipments can be auto assigned."));
        }

        var selection = await _assignmentSelector.SelectAsync(shipment, cancellationToken);
        if (selection.Status == ShipmentAssignmentSelectionStatus.NoEligibleShipper
            || selection.ShipperId is null)
        {
            return Result<AutoAssignShipmentResult>.Success(
                AutoAssignShipmentResult.NoEligibleShipper(shipment, selection.Reason));
        }

        var assignResult = shipment.AssignShipper(
            selection.ShipperId.Value,
            SystemActorIds.AutoAssignment,
            $"Auto assigned. {selection.Reason}");
        if (assignResult.IsFailure)
        {
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

        return Result<AutoAssignShipmentResult>.Success(
            AutoAssignShipmentResult.Assigned(
                shipment,
                selection.ShipperId.Value,
                selection.Reason));
    }
}
