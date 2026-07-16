using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers.SetShipperCapacity;

public sealed class SetShipperCapacityService : ISetShipperCapacityService
{
    private readonly IValidator<SetShipperCapacityCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IApplicationDbTransactionManager? _transactionManager;

    public SetShipperCapacityService(
        IValidator<SetShipperCapacityCommand> validator,
        IIdentityService identityService,
        IAdminAuditService? adminAuditService = null,
        IApplicationDbTransactionManager? transactionManager = null)
    {
        _validator = validator;
        _identityService = identityService;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _transactionManager = transactionManager;
    }

    public async Task<Result> SetAsync(
        SetShipperCapacityCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            command.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return authorizationResult;
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            command.ShipperId,
            nameof(UserRole.Shipper),
            cancellationToken);
        if (!shipperCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipper was not found."));
        }

        if (!shipperCheck.IsInRole)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Selected user is not a shipper."));
        }

        var existingShipper = (await _identityService.GetUsersByIdsAsync(
                [command.ShipperId],
                cancellationToken))
            .FirstOrDefault();

        IApplicationDbTransaction? transaction = null;
        try
        {
            transaction = _transactionManager is null
                ? null
                : await _transactionManager.BeginTransactionAsync(cancellationToken);

            var setResult = await _identityService.SetShipperCapacityAsync(
                command.ShipperId,
                command.IsAvailableForAssignment,
                command.MaxActiveShipments,
                cancellationToken);
            if (setResult.IsFailure)
            {
                return setResult;
            }

            await _adminAuditService.RecordAsync(
                new AdminAuditEntry(
                    command.RequestedByUserId,
                    AdminAuditActions.ShipperCapacityChanged,
                    AdminAuditTargetTypes.Shipper,
                    command.ShipperId,
                    OldValue: existingShipper is null ? null : new
                    {
                        existingShipper.IsAvailableForAssignment,
                        existingShipper.MaxActiveShipments
                    },
                    NewValue: new
                    {
                        command.IsAvailableForAssignment,
                        command.MaxActiveShipments
                    },
                    ActorRole: nameof(UserRole.Admin)),
                cancellationToken);
            await _adminAuditService.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Result.Success();
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
