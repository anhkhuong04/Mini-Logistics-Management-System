using FluentValidation;

namespace MiniLogistics.Application.CashOnDelivery.MarkCodCollected;

public sealed class MarkCodCollectedCommandValidator : AbstractValidator<MarkCodCollectedCommand>
{
    public MarkCodCollectedCommandValidator()
    {
        RuleFor(command => command.ShipmentId)
            .NotEmpty();

        RuleFor(command => command.CollectedByUserId)
            .NotEmpty();
    }
}
