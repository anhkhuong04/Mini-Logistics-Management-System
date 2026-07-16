namespace MiniLogistics.Application.AdminHubs;

public sealed record AdminHubResponse(
    Guid HubId,
    string Code,
    string Name,
    string Province,
    string? Ward,
    string? AddressLine,
    string Country,
    bool IsRegionalSortingHub,
    bool IsActive,
    int ActiveWorkingAreaCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
