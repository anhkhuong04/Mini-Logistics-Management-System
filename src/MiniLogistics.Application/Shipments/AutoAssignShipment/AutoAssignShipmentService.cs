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

    public AutoAssignShipmentService(
        IShipmentRepository shipmentRepository,
        IShipmentAssignmentSelector assignmentSelector,
        IWebhookEventPublisher? webhookEventPublisher = null)
    {
        _shipmentRepository = shipmentRepository;
        _assignmentSelector = assignmentSelector;
        _webhookEventPublisher = webhookEventPublisher ?? NullWebhookEventPublisher.Instance;
    }

    public async Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default)
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
        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result<AutoAssignShipmentResult>.Success(
            AutoAssignShipmentResult.Assigned(
                shipment,
                selection.ShipperId.Value,
                selection.Reason));
    }
}
