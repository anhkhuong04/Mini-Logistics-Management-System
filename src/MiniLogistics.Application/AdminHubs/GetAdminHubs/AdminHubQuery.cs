namespace MiniLogistics.Application.AdminHubs.GetAdminHubs;

public sealed record AdminHubQuery(
    Guid RequestedByUserId,
    string? SearchText = null,
    bool? IsActive = null,
    string? Province = null,
    bool? IsRegionalSortingHub = null);
