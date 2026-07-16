using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminHubs.GetAdminHubs;
using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminHubs.SetHubActiveStatus;

public sealed class SetHubActiveStatusService : ISetHubActiveStatusService
{
    private readonly IValidator<SetHubActiveStatusCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IHubRepository _hubRepository;
    private readonly IShipperWorkingAreaRepository _workingAreaRepository;
    private readonly IAdminAuditService _adminAuditService;

    public SetHubActiveStatusService(
        IValidator<SetHubActiveStatusCommand> validator,
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

    public async Task<Result<AdminHubResponse>> SetAsync(
        SetHubActiveStatusCommand command,
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

        var activeWorkingAreaCount = await _workingAreaRepository.CountActiveByHubIdAsync(
            hub.Id,
            cancellationToken);
        var oldStatus = hub.IsActive;

        if (command.IsActive)
        {
            hub.Activate();
        }
        else
        {
            hub.Deactivate();
        }

        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.RequestedByUserId,
                AdminAuditActions.HubActiveStatusChanged,
                AdminAuditTargetTypes.Hub,
                hub.Id,
                OldValue: new
                {
                    IsActive = oldStatus,
                    ActiveWorkingAreaCount = activeWorkingAreaCount
                },
                NewValue: new
                {
                    hub.IsActive,
                    ActiveWorkingAreaCount = activeWorkingAreaCount
                },
                Reason: command.Reason,
                ActorRole: nameof(UserRole.Admin)),
            cancellationToken);
        await _hubRepository.SaveChangesAsync(cancellationToken);

        return Result<AdminHubResponse>.Success(
            GetAdminHubsService.ToResponse(hub, activeWorkingAreaCount));
    }
}
