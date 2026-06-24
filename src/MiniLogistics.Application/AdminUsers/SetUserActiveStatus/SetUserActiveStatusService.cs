using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers.SetUserActiveStatus;

public sealed class SetUserActiveStatusService : ISetUserActiveStatusService
{
    private readonly IValidator<SetUserActiveStatusCommand> _validator;
    private readonly IIdentityService _identityService;

    public SetUserActiveStatusService(
        IValidator<SetUserActiveStatusCommand> validator,
        IIdentityService identityService)
    {
        _validator = validator;
        _identityService = identityService;
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

        return await _identityService.SetUserActiveStatusAsync(
            command.TargetUserId,
            command.IsActive,
            cancellationToken);
    }
}
