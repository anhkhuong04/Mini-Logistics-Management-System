using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Identity;

public interface IIdentityService
{
    Task<Result<Guid>> CreateUserAsync(
        string fullName,
        string email,
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result> AddToRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);

    Task<Result<Guid>> CreateInternalUserAsync(
        string fullName,
        string email,
        string phoneNumber,
        string password,
        string role,
        CancellationToken cancellationToken = default);

    Task<Result> SetUserActiveStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<Result> SetShipperCapacityAsync(
        Guid userId,
        bool isAvailableForAssignment,
        int maxActiveShipments,
        CancellationToken cancellationToken = default);

    Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IdentityUserSummaryResponse>> GetUsersByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}

public sealed record IdentityUserRoleCheckResponse(
    Guid UserId,
    bool Exists,
    bool IsActive,
    bool IsInRole);

public sealed record ActiveShipperResponse(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsAvailableForAssignment,
    int MaxActiveShipments);

public sealed record IdentityUserSummaryResponse(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsAvailableForAssignment,
    int MaxActiveShipments);

public sealed record IdentityUserWithRolesResponse(
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool IsAvailableForAssignment,
    int MaxActiveShipments,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAtUtc);
