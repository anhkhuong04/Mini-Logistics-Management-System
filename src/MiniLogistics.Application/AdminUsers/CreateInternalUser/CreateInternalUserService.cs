using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers.CreateInternalUser;

public sealed class CreateInternalUserService : ICreateInternalUserService
{
    private readonly IValidator<CreateInternalUserCommand> _validator;
    private readonly IIdentityService _identityService;

    public CreateInternalUserService(
        IValidator<CreateInternalUserCommand> validator,
        IIdentityService identityService)
    {
        _validator = validator;
        _identityService = identityService;
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

        return Result<CreateInternalUserResponse>.Success(new CreateInternalUserResponse(
            createResult.Value,
            command.FullName.Trim(),
            command.Email.Trim(),
            role));
    }

    private static string NormalizeRole(string role)
    {
        return string.Equals(role, nameof(UserRole.Operator), StringComparison.OrdinalIgnoreCase)
            ? nameof(UserRole.Operator)
            : nameof(UserRole.Shipper);
    }
}
