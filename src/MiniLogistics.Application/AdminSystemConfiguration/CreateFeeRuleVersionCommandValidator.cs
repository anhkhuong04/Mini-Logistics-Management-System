using FluentValidation;

namespace MiniLogistics.Application.AdminSystemConfiguration;

public sealed class CreateFeeRuleVersionCommandValidator : AbstractValidator<CreateFeeRuleVersionCommand>
{
    public CreateFeeRuleVersionCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.BaseWeightKg)
            .GreaterThan(0);

        RuleFor(command => command.BaseFeeAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.ExtraWeightStepKg)
            .GreaterThan(0);

        RuleFor(command => command.ExtraStepFeeAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.MinimumWeightKg)
            .GreaterThan(0)
            .When(command => command.MinimumWeightKg.HasValue);

        RuleFor(command => command.MaximumWeightKg)
            .GreaterThan(0)
            .When(command => command.MaximumWeightKg.HasValue);

        RuleFor(command => command)
            .Must(command => !command.MinimumWeightKg.HasValue
                || !command.MaximumWeightKg.HasValue
                || command.MinimumWeightKg.Value <= command.MaximumWeightKg.Value)
            .WithMessage("Minimum weight cannot be greater than maximum weight.");

        RuleFor(command => command.InsuranceFreeThreshold)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.InsuranceMaximumValue)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.InsuranceRate)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.ReturnFeeRate)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command.Reason)
            .MaximumLength(500);
    }
}
