using FluentValidation;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminUsers.SetShipperCapacity;

public sealed class SetShipperCapacityService : ISetShipperCapacityService
{
    private readonly IValidator<SetShipperCapacityCommand> _validator;
    private readonly IIdentityService _identityService;

    public SetShipperCapacityService(
        IValidator<SetShipperCapacityCommand> validator,
        IIdentityService identityService)
    {
        _validator = validator;
        _identityService = identityService;
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

        return await _identityService.SetShipperCapacityAsync(
            command.ShipperId,
            command.IsAvailableForAssignment,
            command.MaxActiveShipments,
            cancellationToken);
    }
}
