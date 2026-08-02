using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace MiniLogistics.Application.Shops.Staff;

public sealed class ShopStaffManagementService : IShopStaffManagementService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IShopStaffMembershipRepository _membershipRepository;
    private readonly IIdentityService _identityService;
    private readonly IAdminAuditService _auditService;
    private readonly TimeProvider _timeProvider;

    public ShopStaffManagementService(
        IShopAccessService shopAccessService,
        IShopStaffMembershipRepository membershipRepository,
        IIdentityService identityService,
        IAdminAuditService auditService,
        TimeProvider timeProvider)
    {
        _shopAccessService = shopAccessService;
        _membershipRepository = membershipRepository;
        _identityService = identityService;
        _auditService = auditService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<ShopStaffResponse>>> GetAsync(
        Guid currentUserId,
        Guid shopId,
        CancellationToken cancellationToken = default)
    {
        var ownerResult = await EnsureOwnerAsync(currentUserId, shopId, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return Result<IReadOnlyList<ShopStaffResponse>>.Failure(ownerResult.Error);
        }

        var memberships = await _membershipRepository.GetByShopIdAsync(shopId, cancellationToken);
        var users = await _identityService.GetUsersByIdsAsync(
            memberships.Select(membership => membership.UserId).ToArray(),
            cancellationToken);
        return Result<IReadOnlyList<ShopStaffResponse>>.Success(memberships
            .Select(membership => ToResponse(
                membership,
                users.FirstOrDefault(user => user.UserId == membership.UserId)))
            .ToList());
    }

    public async Task<Result<ShopStaffResponse>> CreateAsync(
        CreateShopStaffCommand command,
        CancellationToken cancellationToken = default)
    {
        var accountValidation = ValidateAccount(command);
        if (accountValidation.IsFailure)
        {
            return Result<ShopStaffResponse>.Failure(accountValidation.Error);
        }

        var validation = ValidateAccess(command.ShopId, command.Role, command.Permissions);
        if (validation.IsFailure)
        {
            return Result<ShopStaffResponse>.Failure(validation.Error);
        }

        var ownerResult = await EnsureOwnerAsync(command.CurrentUserId, command.ShopId, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return Result<ShopStaffResponse>.Failure(ownerResult.Error);
        }

        var userResult = await _identityService.CreateUserAsync(
            command.FullName,
            command.Email,
            command.PhoneNumber,
            command.Password,
            cancellationToken);
        if (userResult.IsFailure)
        {
            return Result<ShopStaffResponse>.Failure(userResult.Error);
        }

        var roleResult = await _identityService.AddToRoleAsync(
            userResult.Value,
            nameof(UserRole.Shop),
            cancellationToken);
        if (roleResult.IsFailure)
        {
            return Result<ShopStaffResponse>.Failure(roleResult.Error);
        }

        var now = _timeProvider.GetUtcNow();
        var membership = new ShopStaffMembership(
            command.ShopId,
            userResult.Value,
            command.CurrentUserId,
            command.Role,
            command.Permissions,
            now);
        await _membershipRepository.AddAsync(membership, cancellationToken);
        await _auditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.ShopStaffCreated,
                AdminAuditTargetTypes.ShopStaffMembership,
                membership.Id,
                NewValue: new
                {
                    membership.ShopId,
                    membership.UserId,
                    Role = membership.Role.ToString(),
                    Permissions = membership.Permissions.ToString(),
                    membership.IsActive
                }),
            cancellationToken);
        await _membershipRepository.SaveChangesAsync(cancellationToken);

        var user = (await _identityService.GetUsersByIdsAsync([membership.UserId], cancellationToken))
            .FirstOrDefault();
        return Result<ShopStaffResponse>.Success(ToResponse(membership, user));
    }

    public async Task<Result<ShopStaffResponse>> UpdateAsync(
        UpdateShopStaffAccessCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateAccess(command.ShopId, command.Role, command.Permissions);
        if (validation.IsFailure)
        {
            return Result<ShopStaffResponse>.Failure(validation.Error);
        }

        var ownerResult = await EnsureOwnerAsync(command.CurrentUserId, command.ShopId, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return Result<ShopStaffResponse>.Failure(ownerResult.Error);
        }

        var membership = await _membershipRepository.GetByShopAndUserAsync(
            command.ShopId,
            command.StaffUserId,
            cancellationToken);
        if (membership is null)
        {
            return Result<ShopStaffResponse>.Failure(
                ApplicationErrors.NotFound("Shop staff membership was not found."));
        }

        var oldValue = new
        {
            Role = membership.Role.ToString(),
            Permissions = membership.Permissions.ToString(),
            membership.IsActive
        };
        var now = _timeProvider.GetUtcNow();
        membership.SetAccess(command.Role, command.Permissions, now);
        membership.SetActive(command.IsActive, now);
        await _auditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.ShopStaffAccessUpdated,
                AdminAuditTargetTypes.ShopStaffMembership,
                membership.Id,
                OldValue: oldValue,
                NewValue: new
                {
                    Role = membership.Role.ToString(),
                    Permissions = membership.Permissions.ToString(),
                    membership.IsActive
                }),
            cancellationToken);
        await _membershipRepository.SaveChangesAsync(cancellationToken);

        var user = (await _identityService.GetUsersByIdsAsync([membership.UserId], cancellationToken))
            .FirstOrDefault();
        return Result<ShopStaffResponse>.Success(ToResponse(membership, user));
    }

    private async Task<Result<ShopAccessContext>> EnsureOwnerAsync(
        Guid currentUserId,
        Guid shopId,
        CancellationToken cancellationToken)
    {
        var accessResult = await _shopAccessService.GetShopAccessAsync(
            currentUserId,
            shopId,
            requireActiveShop: false,
            ShopPermission.ViewShipments,
            cancellationToken);
        return accessResult.IsFailure
            ? accessResult
            : accessResult.Value.IsOwner
                ? accessResult
                : Result<ShopAccessContext>.Failure(
                    ApplicationErrors.Forbidden("Only the shop owner can manage staff accounts."));
    }

    private static Result ValidateAccess(
        Guid shopId,
        ShopStaffRole role,
        ShopPermission permissions)
    {
        if (shopId == Guid.Empty || !Enum.IsDefined(role))
        {
            return Result.Failure(ApplicationErrors.ValidationFailed("Shop and staff role are required."));
        }

        if ((permissions & ShopPermission.ViewShipments) == 0
            || (permissions & ~ShopPermission.All) != 0)
        {
            return Result.Failure(ApplicationErrors.ValidationFailed(
                "Staff permissions must include ViewShipments and contain only supported values."));
        }

        return role != ShopStaffRole.Custom
               && permissions != ShopStaffPermissionPresets.ForRole(role)
            ? Result.Failure(ApplicationErrors.ValidationFailed(
                "Preset staff roles must use their predefined permissions."))
            : Result.Success();
    }

    private static Result ValidateAccount(CreateShopStaffCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.FullName)
            || string.IsNullOrWhiteSpace(command.Email)
            || string.IsNullOrWhiteSpace(command.PhoneNumber))
        {
            return Result.Failure(ApplicationErrors.ValidationFailed(
                "Staff name, email, and phone number are required."));
        }

        if (!new EmailAddressAttribute().IsValid(command.Email))
        {
            return Result.Failure(ApplicationErrors.ValidationFailed("Staff email is invalid."));
        }

        if (!PasswordPolicy.MeetsComplexity(command.Password))
        {
            return Result.Failure(ApplicationErrors.ValidationFailed(PasswordPolicy.RequirementMessage));
        }

        try
        {
            _ = new PhoneNumber(command.PhoneNumber);
        }
        catch (DomainException exception)
        {
            return Result.Failure(ApplicationErrors.ValidationFailed(exception.Message));
        }

        return Result.Success();
    }

    private static ShopStaffResponse ToResponse(
        ShopStaffMembership membership,
        IdentityUserSummaryResponse? user) =>
        new(
            membership.Id,
            membership.UserId,
            user?.FullName ?? "Unknown user",
            user?.Email ?? string.Empty,
            user?.PhoneNumber,
            membership.Role,
            membership.Permissions,
            membership.IsActive,
            membership.CreatedAtUtc);
}
