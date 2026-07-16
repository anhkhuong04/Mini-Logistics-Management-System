using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers.CreateInternalUser;

public sealed class CreateInternalUserService : ICreateInternalUserService
{
    private readonly IValidator<CreateInternalUserCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IAdminAuditService _adminAuditService;
    private readonly IApplicationDbTransactionManager? _transactionManager;

    public CreateInternalUserService(
        IValidator<CreateInternalUserCommand> validator,
        IIdentityService identityService,
        IAdminAuditService? adminAuditService = null,
        IApplicationDbTransactionManager? transactionManager = null)
    {
        _validator = validator;
        _identityService = identityService;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
        _transactionManager = transactionManager;
    }

    public async Task<Result<CreateInternalUserResponse>> CreateAsync(
        CreateInternalUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<CreateInternalUserResponse>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            command.RequestedByUserId,
            cancellationToken);

        if (authorizationResult.IsFailure)
        {
            return Result<CreateInternalUserResponse>.Failure(authorizationResult.Error);
        }

        IApplicationDbTransaction? transaction = null;
        try
        {
            transaction = _transactionManager is null
                ? null
                : await _transactionManager.BeginTransactionAsync(cancellationToken);

            var role = NormalizeRole(command.Role);
            var createResult = await _identityService.CreateInternalUserAsync(
                command.FullName,
                command.Email,
                command.PhoneNumber,
                command.Password,
                role,
                cancellationToken);

            if (createResult.IsFailure)
            {
                return Result<CreateInternalUserResponse>.Failure(createResult.Error);
            }

            await _adminAuditService.RecordAsync(
                new AdminAuditEntry(
                    command.RequestedByUserId,
                    AdminAuditActions.InternalUserCreated,
                    AdminAuditTargetTypes.User,
                    createResult.Value,
                    NewValue: new
                    {
                        Role = role,
                        IsActive = true
                    },
                    ActorRole: nameof(UserRole.Admin)),
                cancellationToken);
            await _adminAuditService.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Result<CreateInternalUserResponse>.Success(new CreateInternalUserResponse(
                createResult.Value,
                command.FullName.Trim(),
                command.Email.Trim(),
                role));
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static string NormalizeRole(string role)
    {
        return string.Equals(role, nameof(UserRole.Operator), StringComparison.OrdinalIgnoreCase)
            ? nameof(UserRole.Operator)
            : nameof(UserRole.Shipper);
    }
}
