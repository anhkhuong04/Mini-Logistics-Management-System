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

        RuleFor(command => command.CollectedAmount)
            .GreaterThanOrEqualTo(0m)
            .When(command => command.CollectedAmount.HasValue);

        RuleFor(command => command.Note)
            .MaximumLength(500);
    }
}
