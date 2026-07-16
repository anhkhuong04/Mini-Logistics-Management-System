using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminHubs.GetAdminHubs;
using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminHubs.UpdateHub;

public sealed class UpdateHubService : IUpdateHubService
{
    private readonly IValidator<UpdateHubCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IHubRepository _hubRepository;
    private readonly IShipperWorkingAreaRepository _workingAreaRepository;
    private readonly IAdminAuditService _adminAuditService;

    public UpdateHubService(
        IValidator<UpdateHubCommand> validator,
        IIdentityService identityService,
        IHubRepository hubRepository,
        IShipperWorkingAreaRepository workingAreaRepository,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _hubRepository = hubRepository;
        _workingAreaRepository = workingAreaRepository;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result<AdminHubResponse>> UpdateAsync(
        UpdateHubCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<AdminHubResponse>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            command.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<AdminHubResponse>.Failure(authorizationResult.Error);
        }

        var hub = await _hubRepository.GetByIdAsync(command.HubId, cancellationToken);
        if (hub is null)
        {
            return Result<AdminHubResponse>.Failure(ApplicationErrors.NotFound("Hub was not found."));
        }

        var duplicateCodeHub = await _hubRepository.GetByCodeAsync(command.Code, cancellationToken);
        if (duplicateCodeHub is not null && duplicateCodeHub.Id != hub.Id)
        {
            return Result<AdminHubResponse>.Failure(ApplicationErrors.Conflict("Hub code already exists."));
        }

        var oldValue = ToAuditValue(hub);
        hub.UpdateProfile(
            command.Code,
            command.Name,
            command.Province,
            command.Ward,
            command.AddressLine,
            command.IsRegionalSortingHub,
            command.Country);

        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.RequestedByUserId,
                AdminAuditActions.HubUpdated,
                AdminAuditTargetTypes.Hub,
                hub.Id,
                OldValue: oldValue,
                NewValue: ToAuditValue(hub),
                ActorRole: nameof(UserRole.Admin)),
            cancellationToken);
        await _hubRepository.SaveChangesAsync(cancellationToken);

        var activeWorkingAreaCount = await _workingAreaRepository.CountActiveByHubIdAsync(
            hub.Id,
            cancellationToken);
        return Result<AdminHubResponse>.Success(
            GetAdminHubsService.ToResponse(hub, activeWorkingAreaCount));
    }

    private static object ToAuditValue(Hub hub)
    {
        return new
        {
            hub.Code,
            hub.Name,
            hub.Province,
            hub.Ward,
            hub.AddressLine,
            hub.Country,
            hub.IsRegionalSortingHub,
            hub.IsActive
        };
    }
}
