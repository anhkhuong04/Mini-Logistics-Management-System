using FluentValidation;

namespace MiniLogistics.Application.Shops.SetShopActiveStatus;

public sealed class SetShopActiveStatusCommandValidator : AbstractValidator<SetShopActiveStatusCommand>
{
    public SetShopActiveStatusCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty()
            .WithMessage("Requester user id is required.");

        RuleFor(command => command.ShopId)
            .NotEmpty()
            .WithMessage("Shop id is required.");
    }
}
