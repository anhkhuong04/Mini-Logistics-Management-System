using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.SetShopActiveStatus;

public sealed class SetShopActiveStatusService : ISetShopActiveStatusService
{
    private readonly IValidator<SetShopActiveStatusCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IShopRepository _shopRepository;
    private readonly IAdminAuditService _adminAuditService;

    public SetShopActiveStatusService(
        IValidator<SetShopActiveStatusCommand> validator,
        IIdentityService identityService,
        IShopRepository shopRepository,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _shopRepository = shopRepository;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result> SetAsync(
        SetShopActiveStatusCommand command,
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

        var shop = await _shopRepository.GetByIdAsync(command.ShopId, cancellationToken);
        if (shop is null)
        {
            return Result.Failure(ApplicationErrors.NotFound("Shop was not found."));
        }

        var oldIsActive = shop.IsActive;

        if (command.IsActive)
        {
            shop.Activate();
        }
        else
        {
            shop.Deactivate();
        }

        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.RequestedByUserId,
                AdminAuditActions.ShopActiveStatusChanged,
                AdminAuditTargetTypes.Shop,
                shop.Id,
                OldValue: new
                {
                    IsActive = oldIsActive
                },
                NewValue: new
                {
                    shop.IsActive
                },
                ActorRole: "Admin"),
            cancellationToken);
        await _shopRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
