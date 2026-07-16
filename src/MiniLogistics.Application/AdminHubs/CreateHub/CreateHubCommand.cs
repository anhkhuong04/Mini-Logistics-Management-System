namespace MiniLogistics.Application.AdminHubs.CreateHub;

public sealed record CreateHubCommand(
    Guid RequestedByUserId,
    string Code,
    string Name,
    string Province,
    string? Ward,
    string? AddressLine,
    string Country,
    bool IsRegionalSortingHub);
