using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Application.AdminHubs.GetAdminHubs;

public sealed class GetAdminHubsService : IGetAdminHubsService
{
    private readonly IIdentityService _identityService;
    private readonly IHubRepository _hubRepository;
    private readonly IShipperWorkingAreaRepository _workingAreaRepository;

    public GetAdminHubsService(
        IIdentityService identityService,
        IHubRepository hubRepository,
        IShipperWorkingAreaRepository workingAreaRepository)
    {
        _identityService = identityService;
        _hubRepository = hubRepository;
        _workingAreaRepository = workingAreaRepository;
    }

    public async Task<Result<IReadOnlyList<AdminHubResponse>>> GetAsync(
        AdminHubQuery query,
        CancellationToken cancellationToken = default)
    {
        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            query.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyList<AdminHubResponse>>.Failure(authorizationResult.Error);
        }

        var hubs = await _hubRepository.GetAllAsync(activeOnly: false, cancellationToken);
        var filteredHubs = hubs
            .Where(hub => Matches(query, hub))
            .OrderByDescending(hub => hub.IsActive)
            .ThenByDescending(hub => hub.IsRegionalSortingHub)
            .ThenBy(hub => hub.Province)
            .ThenBy(hub => hub.Name)
            .ToList();

        var response = new List<AdminHubResponse>();
        foreach (var hub in filteredHubs)
        {
            var activeWorkingAreaCount = await _workingAreaRepository.CountActiveByHubIdAsync(
                hub.Id,
                cancellationToken);
            response.Add(ToResponse(hub, activeWorkingAreaCount));
        }

        return Result<IReadOnlyList<AdminHubResponse>>.Success(response);
    }

    internal static AdminHubResponse ToResponse(Hub hub, int activeWorkingAreaCount)
    {
        return new AdminHubResponse(
            hub.Id,
            hub.Code,
            hub.Name,
            hub.Province,
            hub.Ward,
            hub.AddressLine,
            hub.Country,
            hub.IsRegionalSortingHub,
            hub.IsActive,
            activeWorkingAreaCount,
            hub.CreatedAtUtc,
            hub.UpdatedAtUtc);
    }

    private static bool Matches(AdminHubQuery query, Hub hub)
    {
        if (query.IsActive.HasValue && hub.IsActive != query.IsActive.Value)
        {
            return false;
        }

        if (query.IsRegionalSortingHub.HasValue
            && hub.IsRegionalSortingHub != query.IsRegionalSortingHub.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Province)
            && !string.Equals(hub.Province, query.Province.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.SearchText))
        {
            return true;
        }

        var keyword = query.SearchText.Trim();
        return hub.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || hub.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || hub.Province.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || (hub.Ward?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            || (hub.AddressLine?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
