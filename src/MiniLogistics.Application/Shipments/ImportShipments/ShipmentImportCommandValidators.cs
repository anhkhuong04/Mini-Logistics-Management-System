using FluentValidation;

namespace MiniLogistics.Application.Shipments.ImportShipments;

public sealed class PreviewShipmentImportCommandValidator : AbstractValidator<PreviewShipmentImportCommand>
{
    public PreviewShipmentImportCommandValidator()
    {
        RuleFor(command => command.CurrentUserId)
            .NotEmpty();

        RuleFor(command => command.CsvContent)
            .NotEmpty()
            .MaximumLength(2_000_000);
    }
}

public sealed class ConfirmShipmentImportCommandValidator : AbstractValidator<ConfirmShipmentImportCommand>
{
    public ConfirmShipmentImportCommandValidator()
    {
        RuleFor(command => command.CurrentUserId)
            .NotEmpty();

        RuleFor(command => command.Rows)
            .NotNull()
            .Must(rows => rows.Count is > 0 and <= ShipmentImportService.MaxImportRows)
            .WithMessage($"Import batch must contain between 1 and {ShipmentImportService.MaxImportRows} rows.");
    }
}
