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
    }
}
