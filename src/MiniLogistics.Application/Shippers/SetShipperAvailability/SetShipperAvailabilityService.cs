using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shippers.SetShipperAvailability;

public sealed class SetShipperAvailabilityService : ISetShipperAvailabilityService
{
    private readonly IValidator<SetShipperAvailabilityCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IAdminAuditService _adminAuditService;

    public SetShipperAvailabilityService(
        IValidator<SetShipperAvailabilityCommand> validator,
        IIdentityService identityService,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result> SetAsync(
        SetShipperAvailabilityCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            command.ShipperUserId,
            nameof(UserRole.Shipper),
            cancellationToken);
        if (!shipperCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipper was not found."));
        }

        if (!shipperCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Shipper is not active."));
        }

        if (!shipperCheck.IsInRole)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Only Shipper can change own availability."));
        }

        var userSummary = (await _identityService.GetUsersByIdsAsync([command.ShipperUserId], cancellationToken))
            .FirstOrDefault(user => user.UserId == command.ShipperUserId);
        if (userSummary is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shipper was not found."));
        }

        var oldValue = new
        {
            userSummary.IsAvailableForAssignment,
            userSummary.MaxActiveShipments
        };
        var updateResult = await _identityService.SetShipperCapacityAsync(
            command.ShipperUserId,
            command.IsAvailableForAssignment,
            userSummary.MaxActiveShipments,
            cancellationToken);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.ShipperUserId,
                AdminAuditActions.ShipperAvailabilityChanged,
                AdminAuditTargetTypes.Shipper,
                command.ShipperUserId,
                OldValue: oldValue,
                NewValue: new
                {
                    command.IsAvailableForAssignment,
                    userSummary.MaxActiveShipments
                },
                ActorRole: nameof(UserRole.Shipper)),
            cancellationToken);
        await _adminAuditService.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
