using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.Shippers.SetShipperWorkingAreas;

public sealed class SetShipperWorkingAreasService : ISetShipperWorkingAreasService
{
    private readonly IValidator<SetShipperWorkingAreasCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IHubRepository _hubRepository;
    private readonly IShipperWorkingAreaRepository _workingAreaRepository;
    private readonly IAdminAuditService _adminAuditService;

    public SetShipperWorkingAreasService(
        IValidator<SetShipperWorkingAreasCommand> validator,
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

    public async Task<Result<IReadOnlyList<ShipperWorkingAreaResponse>>> SetAsync(
        SetShipperWorkingAreasCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(
                ApplicationErrors.ValidationFailed(description));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            command.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(authorizationResult.Error);
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            command.ShipperId,
            nameof(UserRole.Shipper),
            cancellationToken);
        if (!shipperCheck.Exists)
        {
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(
                ApplicationErrors.NotFound("Shipper was not found."));
        }

        if (!shipperCheck.IsInRole)
        {
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(
                ApplicationErrors.Forbidden("Selected user is not a shipper."));
        }

        var requestedAreas = NormalizeRequestedAreas(command.Areas);
        if (requestedAreas.IsFailure)
        {
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(requestedAreas.Error);
        }

        var hubIds = requestedAreas.Value.Select(area => area.HubId).Distinct().ToList();
        var hubs = await _hubRepository.GetByIdsAsync(hubIds, cancellationToken);
        var hubById = hubs.ToDictionary(hub => hub.Id);
        var missingHubIds = hubIds.Where(hubId => !hubById.ContainsKey(hubId)).ToList();
        if (missingHubIds.Count > 0)
        {
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(
                ApplicationErrors.NotFound("One or more hubs were not found."));
        }

        if (hubs.Any(hub => !hub.IsActive))
        {
            return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Failure(
                ApplicationErrors.Forbidden("Inactive hubs cannot be assigned to shippers."));
        }

        var currentAreas = await _workingAreaRepository.GetByShipperIdAsync(
            command.ShipperId,
            activeOnly: false,
            cancellationToken);
        var oldActiveAreas = currentAreas
            .Where(area => area.IsActive)
            .Select(area => new
            {
                area.HubId,
                area.Ward,
                area.ZoneCode
            })
            .ToList();
        var addedAreas = new List<ShipperWorkingArea>();

        foreach (var currentArea in currentAreas.Where(area => area.IsActive))
        {
            if (!requestedAreas.Value.Any(area => currentArea.Matches(area.HubId, area.Ward, area.ZoneCode)))
            {
                currentArea.Deactivate();
            }
        }

        foreach (var requestedArea in requestedAreas.Value)
        {
            var existingArea = currentAreas.FirstOrDefault(area =>
                area.Matches(requestedArea.HubId, requestedArea.Ward, requestedArea.ZoneCode));

            if (existingArea is not null)
            {
                existingArea.Activate();
                continue;
            }

            var hub = hubById[requestedArea.HubId];
            var newArea = new ShipperWorkingArea(
                command.ShipperId,
                hub.Id,
                hub.Province,
                requestedArea.Ward,
                requestedArea.ZoneCode);
            addedAreas.Add(newArea);
            await _workingAreaRepository.AddAsync(newArea, cancellationToken);
        }

        var activeAreas = currentAreas
            .Concat(addedAreas)
            .Where(area => area.IsActive)
            .ToList();

        var response = ShipperWorkingAreaResponseMapper.ToResponses(
            activeAreas,
            hubById);

        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.RequestedByUserId,
                AdminAuditActions.ShipperWorkingAreasChanged,
                AdminAuditTargetTypes.Shipper,
                command.ShipperId,
                OldValue: new
                {
                    Areas = oldActiveAreas
                },
                NewValue: new
                {
                    Areas = response.Select(area => new
                    {
                        area.HubId,
                        area.Ward,
                        area.ZoneCode,
                        area.IsActive
                    })
                },
                ActorRole: nameof(UserRole.Admin)),
            cancellationToken);
        await _workingAreaRepository.SaveChangesAsync(cancellationToken);

        return Result<IReadOnlyList<ShipperWorkingAreaResponse>>.Success(response);
    }

    private static Result<IReadOnlyList<NormalizedWorkingArea>> NormalizeRequestedAreas(
        IReadOnlyList<SetShipperWorkingAreaItem> areas)
    {
        var normalizedAreas = areas
            .Select(area => new NormalizedWorkingArea(
                area.HubId,
                NormalizeOptional(area.Ward),
                NormalizeOptional(area.ZoneCode)))
            .Distinct()
            .ToList();

        if (normalizedAreas.Count != areas.Count)
        {
            return Result<IReadOnlyList<NormalizedWorkingArea>>.Failure(
                ApplicationErrors.ValidationFailed("Working areas cannot contain duplicates."));
        }

        return Result<IReadOnlyList<NormalizedWorkingArea>>.Success(normalizedAreas);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed record NormalizedWorkingArea(
        Guid HubId,
        string? Ward,
        string? ZoneCode);
}
