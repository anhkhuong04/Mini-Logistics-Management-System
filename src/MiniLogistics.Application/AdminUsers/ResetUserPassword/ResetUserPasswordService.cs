using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers.ResetUserPassword;

public sealed class ResetUserPasswordService : IResetUserPasswordService
{
    private readonly IValidator<ResetUserPasswordCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IUserPasswordService _userPasswordService;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IApplicationDbTransactionManager? _transactionManager;

    public ResetUserPasswordService(
        IValidator<ResetUserPasswordCommand> validator,
        IIdentityService identityService,
        IUserPasswordService userPasswordService,
        IAdminAuditService? adminAuditService = null,
        IApplicationDbTransactionManager? transactionManager = null)
    {
        _validator = validator;
        _identityService = identityService;
        _userPasswordService = userPasswordService;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _transactionManager = transactionManager;
    }

    public async Task<Result> ResetAsync(
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(ApplicationErrors.ValidationFailed(
                string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage))));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            command.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return authorizationResult;
        }

        var targetUser = (await _identityService.GetUsersByIdsAsync(
                [command.TargetUserId],
                cancellationToken))
            .FirstOrDefault();
        if (targetUser is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Target user was not found."));
        }

        IApplicationDbTransaction? transaction = null;
        try
        {
            transaction = _transactionManager is null
                ? null
                : await _transactionManager.BeginTransactionAsync(cancellationToken);

            var resetResult = await _userPasswordService.ResetPasswordAsync(
                command.TargetUserId,
                command.NewPassword,
                cancellationToken);
            if (resetResult.IsFailure)
            {
                return resetResult;
            }

            await _adminAuditService.RecordAsync(
                new AdminAuditEntry(
                    command.RequestedByUserId,
                    AdminAuditActions.UserPasswordReset,
                    AdminAuditTargetTypes.User,
                    command.TargetUserId,
                    NewValue: new { PasswordReset = true },
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
