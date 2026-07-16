using FluentValidation;

namespace MiniLogistics.Application.CashOnDelivery.MarkCodSettled;

public sealed class MarkCodSettledCommandValidator : AbstractValidator<MarkCodSettledCommand>
{
    public MarkCodSettledCommandValidator()
    {
        RuleFor(command => command.ShipmentId)
            .NotEmpty();

        RuleFor(command => command.SettledByUserId)
            .NotEmpty();

        RuleFor(command => command.Note)
            .MaximumLength(500);
    }
}
