namespace MiniLogistics.Application.AdminDashboard;

public sealed record AdminDashboardQuery(
    Guid RequestedByUserId,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? Province = null);
