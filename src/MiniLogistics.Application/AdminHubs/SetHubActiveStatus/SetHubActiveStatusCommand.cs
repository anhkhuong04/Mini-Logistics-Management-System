namespace MiniLogistics.Application.AdminHubs.SetHubActiveStatus;

public sealed record SetHubActiveStatusCommand(
    Guid RequestedByUserId,
    Guid HubId,
    bool IsActive,
    string? Reason = null);
