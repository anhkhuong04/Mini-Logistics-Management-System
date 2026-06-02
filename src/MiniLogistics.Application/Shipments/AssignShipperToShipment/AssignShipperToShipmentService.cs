using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.AssignShipperToShipment;

public sealed class AssignShipperToShipmentService : IAssignShipperToShipmentService
{
    private readonly IValidator<AssignShipperCommand> _validator;
    private readonly IShipmentRepository _shipmentRepository;

    public AssignShipperToShipmentService(
        IValidator<AssignShipperCommand> validator,
        IShipmentRepository shipmentRepository)
    {
        _validator = validator;
        _shipmentRepository = shipmentRepository;
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
            command.Note);

        if (assignResult.IsFailure)
        {
            return assignResult;
        }

        await _shipmentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
