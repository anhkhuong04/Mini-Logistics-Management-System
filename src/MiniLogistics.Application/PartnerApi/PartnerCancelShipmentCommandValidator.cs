using FluentValidation;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerCancelShipmentCommandValidator : AbstractValidator<PartnerCancelShipmentCommand>
{
    public PartnerCancelShipmentCommandValidator()
    {
        RuleFor(command => command.ApiClientId)
            .NotEmpty();

        RuleFor(command => command.ShopId)
            .NotEmpty();

        RuleFor(command => command.TrackingCode)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}
