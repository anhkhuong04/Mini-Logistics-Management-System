using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.CashOnDelivery.MarkCodCollected;

public sealed class MarkCodCollectedService : IMarkCodCollectedService
{
    private readonly IValidator<MarkCodCollectedCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IOperationAuthorizationService _operationAuthorizationService;
    private readonly TimeProvider _timeProvider;

    public MarkCodCollectedService(
        IValidator<MarkCodCollectedCommand> validator,
        IIdentityService identityService,
        IShipmentRepository shipmentRepository,
        ICodTransactionRepository codTransactionRepository,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null,
        IOperationAuthorizationService? operationAuthorizationService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _shipmentRepository = shipmentRepository;
        _codTransactionRepository = codTransactionRepository;
        _timeProvider = timeProvider;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _operationAuthorizationService = operationAuthorizationService ?? new OperationAuthorizationService(identityService);
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
        var collectedAmount = command.CollectedAmount.HasValue
            ? new Money(command.CollectedAmount.Value, codTransaction.Amount.Currency)
            : null;
        var collectResult = codTransaction.MarkCollected(
            shipment.Status,
            command.CollectedByUserId,
            now,
            collectedAmount,
            command.Note);

        if (collectResult.IsFailure)
        {
            return collectResult;
        }

        var completeShipmentCodCollectionResult = shipment.CompleteCodCollection(now);
        if (completeShipmentCodCollectionResult.IsFailure)
        {
            return completeShipmentCodCollectionResult;
        }

        var auditAction = await ResolveCollectionAuditActionAsync(command.CollectedByUserId, cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CollectedByUserId,
                auditAction,
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
                    CollectedByUserId = command.CollectedByUserId,
                    DeclaredAmount = codTransaction.Amount.Amount,
                    CollectedAmount = codTransaction.CollectedAmount?.Amount,
                    DiscrepancyAmount = codTransaction.DiscrepancyAmount?.Amount
                },
                Reason: command.Note),
            cancellationToken);
        await _codTransactionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<string> ResolveCollectionAuditActionAsync(
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var operatorCheck = await _identityService.CheckUserRoleAsync(
            actorUserId,
            nameof(UserRole.Operator),
            cancellationToken);
        if (operatorCheck.Exists && operatorCheck.IsInRole)
        {
            return AdminAuditActions.CodCollectedByOperator;
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            actorUserId,
            nameof(UserRole.Shipper),
            cancellationToken);

        return shipperCheck.Exists && shipperCheck.IsInRole
            ? AdminAuditActions.CodCollectedByShipper
            : AdminAuditActions.CodCollected;
    }

    private async Task<Result> ValidateCollectionPermissionAsync(
        Guid collectedByUserId,
        Shipment shipment,
        CancellationToken cancellationToken)
    {
        var operationPermission = await _operationAuthorizationService.EnsurePermissionAsync(
            collectedByUserId,
            OperationPermissions.CodCollect,
            "COD collection user was not found.",
            "COD collection user is not active.",
            "Only Admin, Operator or assigned Shipper can confirm COD collection.",
            cancellationToken);

        if (operationPermission.IsSuccess)
        {
            return Result.Success();
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            collectedByUserId,
            nameof(UserRole.Shipper),
            cancellationToken);

        if (!shipperCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("COD collection user was not found."));
        }

        if (!shipperCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("COD collection user is not active."));
        }

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
