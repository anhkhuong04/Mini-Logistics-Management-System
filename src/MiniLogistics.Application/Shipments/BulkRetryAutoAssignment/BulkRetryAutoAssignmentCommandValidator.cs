using FluentValidation;

namespace MiniLogistics.Application.Shipments.BulkRetryAutoAssignment;

public sealed class BulkRetryAutoAssignmentCommandValidator : AbstractValidator<BulkRetryAutoAssignmentCommand>
{
    public const int MaxBatchSize = 50;

    public BulkRetryAutoAssignmentCommandValidator()
    {
        RuleFor(command => command.RequestedByUserId)
            .NotEmpty();

        RuleFor(command => command.ShipmentIds)
            .NotEmpty()
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Shipment ids must be unique.")
            .Must(ids => ids.Count <= MaxBatchSize)
            .WithMessage($"Bulk retry is limited to {MaxBatchSize} shipments.");
    }
}
