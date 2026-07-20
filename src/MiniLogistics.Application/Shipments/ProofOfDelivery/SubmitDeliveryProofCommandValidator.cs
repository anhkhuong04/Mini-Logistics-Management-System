using FluentValidation;

namespace MiniLogistics.Application.Shipments.ProofOfDelivery;

public sealed class SubmitDeliveryProofCommandValidator : AbstractValidator<SubmitDeliveryProofCommand>
{
    public SubmitDeliveryProofCommandValidator()
    {
        RuleFor(command => command.ShipmentId)
            .NotEmpty();

        RuleFor(command => command.SubmittedByUserId)
            .NotEmpty();

        RuleFor(command => command.ProofType)
            .IsInEnum();

        RuleFor(command => command.ProofMethod)
            .IsInEnum();

        RuleFor(command => command.ResourceUri)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(command => command.RecipientName)
            .MaximumLength(150);

        RuleFor(command => command.VerificationText)
            .MaximumLength(150);

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
