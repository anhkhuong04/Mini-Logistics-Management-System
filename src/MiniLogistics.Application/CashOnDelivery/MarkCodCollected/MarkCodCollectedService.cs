using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.CashOnDelivery.MarkCodCollected;

public sealed class MarkCodCollectedService : IMarkCodCollectedService
{
    private readonly IValidator<MarkCodCollectedCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;

    public MarkCodCollectedService(
        IValidator<MarkCodCollectedCommand> validator,
        IIdentityService identityService,
        IShipmentRepository shipmentRepository,
        ICodTransactionRepository codTransactionRepository,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _codTransactionRepository = codTransactionRepository;
        _timeProvider = timeProvider;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result> MarkCollectedAsync(
        MarkCodCollectedCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var shipment = await _shipmentRepository.GetTrackedByIdAsync(
            command.ShipmentId,
            cancellationToken);

        if (shipment is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipment was not found."));
        }

        var authorizationResult = await ValidateCollectionPermissionAsync(
            command.CollectedByUserId,
            shipment,
            cancellationToken);

        if (authorizationResult.IsFailure)
        {
            return authorizationResult;
        }

        var codTransaction = await _codTransactionRepository.GetTrackedByShipmentIdAsync(
            command.ShipmentId,
            cancellationToken);

        if (codTransaction is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("COD transaction was not found."));
        }

        var oldCodStatus = codTransaction.Status;
        var now = _timeProvider.GetUtcNow();
        var collectResult = codTransaction.MarkCollected(
            shipment.Status,
            command.CollectedByUserId,
            now);

        if (collectResult.IsFailure)
        {
            return collectResult;
        }

        var completeShipmentCodCollectionResult = shipment.CompleteCodCollection(now);
        if (completeShipmentCodCollectionResult.IsFailure)
        {
            return completeShipmentCodCollectionResult;
        }

        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CollectedByUserId,
                AdminAuditActions.CodCollected,
                AdminAuditTargetTypes.CodTransaction,
                codTransaction.Id,
                OldValue: new
                {
                    Status = oldCodStatus.ToString()
                },
                NewValue: new
                {
                    Status = codTransaction.Status.ToString(),
                    codTransaction.ShipmentId,
                    CollectedByUserId = command.CollectedByUserId
                }),
            cancellationToken);
        await _codTransactionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateCollectionPermissionAsync(
        Guid collectedByUserId,
        Shipment shipment,
        CancellationToken cancellationToken)
    {
        var adminCheck = await _identityService.CheckUserRoleAsync(
            collectedByUserId,
            nameof(UserRole.Admin),
            cancellationToken);

        if (!adminCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("COD collection user was not found."));
        }

        if (!adminCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("COD collection user is not active."));
        }

        if (adminCheck.IsInRole)
        {
            return Result.Success();
        }

        var operatorCheck = await _identityService.CheckUserRoleAsync(
            collectedByUserId,
            nameof(UserRole.Operator),
            cancellationToken);

        if (operatorCheck.IsInRole)
        {
            return Result.Success();
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            collectedByUserId,
            nameof(UserRole.Shipper),
            cancellationToken);

        if (!shipperCheck.IsInRole)
        {
            return Result.Failure(ApplicationErrors.Forbidden(
                "Only Admin, Operator or assigned Shipper can confirm COD collection."));
        }

        var hasActiveAssignment = shipment.Assignments.Any(assignment =>
            assignment.IsActive && assignment.ShipperId == collectedByUserId);

        return hasActiveAssignment
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden(
                "Shipper can only confirm COD collection for shipments assigned to them."));
    }
}
