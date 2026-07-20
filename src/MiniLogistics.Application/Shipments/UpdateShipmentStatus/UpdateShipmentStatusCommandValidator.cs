using FluentValidation;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.UpdateShipmentStatus;

public sealed class UpdateShipmentStatusCommandValidator : AbstractValidator<UpdateShipmentStatusCommand>
{
    public UpdateShipmentStatusCommandValidator()
    {
        RuleFor(command => command.ShipmentId)
            .NotEmpty();

        RuleFor(command => command.ChangedByUserId)
            .NotEmpty();

        RuleFor(command => command.NewStatus)
            .IsInEnum();

        RuleFor(command => command.Note)
            .MaximumLength(500);

        RuleFor(command => command.Note)
            .NotEmpty()
            .When(command => command.NewStatus == ShipmentStatus.DeliveryFailed)
            .WithMessage("Delivery failure reason is required.");

        RuleFor(command => command.FailureReasonCode)
            .IsInEnum()
            .When(command => command.FailureReasonCode.HasValue);

        RuleFor(command => command.GpsCoordinate!.Latitude)
            .InclusiveBetween(-90m, 90m)
            .When(command => command.GpsCoordinate is not null);

        RuleFor(command => command.GpsCoordinate!.Longitude)
            .InclusiveBetween(-180m, 180m)
            .When(command => command.GpsCoordinate is not null);

        RuleFor(command => command.GpsCoordinate!.AccuracyMeters)
            .GreaterThanOrEqualTo(0m)
            .When(command => command.GpsCoordinate?.AccuracyMeters is not null);
    }
}
