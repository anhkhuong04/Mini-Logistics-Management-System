using FluentValidation;

namespace MiniLogistics.Application.AdminHubs.CreateHub;

public sealed class CreateHubCommandValidator : AbstractValidator<CreateHubCommand>
{
    public CreateHubCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(40);

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Province)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Ward)
            .MaximumLength(100);

        RuleFor(command => command.AddressLine)
            .MaximumLength(300);

        RuleFor(command => command.Country)
            .NotEmpty()
            .MaximumLength(100);
    }
}
