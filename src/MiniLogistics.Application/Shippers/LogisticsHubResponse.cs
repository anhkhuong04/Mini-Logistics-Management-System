namespace MiniLogistics.Application.Shippers;

public sealed record LogisticsHubResponse(
    Guid HubId,
    string Code,
    string Name,
    string Province,
    string? Ward,
    string? AddressLine,
    string Country,
    bool IsRegionalSortingHub,
    bool IsActive);
