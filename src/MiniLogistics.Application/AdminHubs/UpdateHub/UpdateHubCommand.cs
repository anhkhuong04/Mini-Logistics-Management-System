namespace MiniLogistics.Application.AdminHubs.UpdateHub;

public sealed record UpdateHubCommand(
    Guid RequestedByUserId,
    Guid HubId,
    string Code,
    string Name,
    string Province,
    string? Ward,
    string? AddressLine,
    string Country,
    bool IsRegionalSortingHub);
