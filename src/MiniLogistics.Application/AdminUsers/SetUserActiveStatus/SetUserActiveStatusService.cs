using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers.SetUserActiveStatus;

public sealed class SetUserActiveStatusService : ISetUserActiveStatusService
{
    private readonly IValidator<SetUserActiveStatusCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IApplicationDbTransactionManager? _transactionManager;

    public SetUserActiveStatusService(
        IValidator<SetUserActiveStatusCommand> validator,
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
        SetUserActiveStatusCommand command,
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
            command.TargetUserId,
            nameof(UserRole.Shipper),
            cancellationToken);

        if (!shipperCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Target user was not found."));
        }

        var operatorCheck = await _identityService.CheckUserRoleAsync(
            command.TargetUserId,
            nameof(UserRole.Operator),
            cancellationToken);

        if (!shipperCheck.IsInRole && !operatorCheck.IsInRole)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Only Shipper or Operator accounts can be activated or deactivated."));
        }

        var existingUser = (await _identityService.GetUsersByIdsAsync(
                [command.TargetUserId],
                cancellationToken))
            .FirstOrDefault();

        IApplicationDbTransaction? transaction = null;
        try
        {
            transaction = _transactionManager is null
                ? null
                : await _transactionManager.BeginTransactionAsync(cancellationToken);

            var setResult = await _identityService.SetUserActiveStatusAsync(
                command.TargetUserId,
                command.IsActive,
                cancellationToken);
            if (setResult.IsFailure)
            {
                return setResult;
            }

            await _adminAuditService.RecordAsync(
                new AdminAuditEntry(
                    command.RequestedByUserId,
                    AdminAuditActions.UserActiveStatusChanged,
                    AdminAuditTargetTypes.User,
                    command.TargetUserId,
                    OldValue: existingUser is null ? null : new
                    {
                        existingUser.IsActive
                    },
                    NewValue: new
                    {
                        command.IsActive
                    },
                    Reason: command.Reason,
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
