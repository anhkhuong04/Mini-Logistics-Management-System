using FluentValidation;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerGetShipmentCommandValidator : AbstractValidator<PartnerGetShipmentCommand>
{
    public PartnerGetShipmentCommandValidator()
    {
        RuleFor(command => command.ApiClientId)
            .NotEmpty();

        RuleFor(command => command.ShopId)
            .NotEmpty();

        RuleFor(command => command.TrackingCode)
            .NotEmpty()
            .MaximumLength(50);
    }
}
