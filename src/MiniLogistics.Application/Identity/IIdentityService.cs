using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Identity;

/// <summary>
/// Defines the application boundary for user identity, role membership, and operational user metadata.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Creates a shop-facing user account with validated credentials.
    /// </summary>
    /// <param name="fullName">The user's display name.</param>
    /// <param name="email">The unique email address used for sign-in.</param>
    /// <param name="phoneNumber">The user's contact phone number.</param>
    /// <param name="password">The initial password that must satisfy the configured password policy.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<Guid>> CreateUserAsync(
        string fullName,
        string email,
        string phoneNumber,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an existing user to an application role.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="role">The role name to assign.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result> AddToRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an internal staff account and assigns its initial role in one use case.
    /// </summary>
    /// <param name="fullName">The user's display name.</param>
    /// <param name="email">The unique email address used for sign-in.</param>
    /// <param name="phoneNumber">The user's contact phone number.</param>
    /// <param name="password">The initial password that must satisfy the configured password policy.</param>
    /// <param name="role">The internal role to assign.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<Guid>> CreateInternalUserAsync(
        string fullName,
        string email,
        string phoneNumber,
        string password,
        string role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates or deactivates an application user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="isActive">Whether the user should be allowed to operate in the system.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result> SetUserActiveStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates shipper availability and maximum active shipment capacity.
    /// </summary>
    /// <param name="userId">The shipper user identifier.</param>
    /// <param name="isAvailableForAssignment">Whether automatic/manual assignment may select this shipper.</param>
    /// <param name="maxActiveShipments">The maximum concurrent active shipments allowed for the shipper.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result> SetShipperCapacityAsync(
        Guid userId,
        bool isAvailableForAssignment,
        int maxActiveShipments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a user exists, is active, and belongs to a specific role.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="role">The role name to verify.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IdentityUserRoleCheckResponse> CheckUserRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists users with their role names for admin user management screens.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<IdentityUserWithRolesResponse>> ListUsersWithRolesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active shippers that can be considered for assignment workflows.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<IReadOnlyList<ActiveShipperResponse>> GetActiveShippersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads compact user summaries for the requested identifiers.
    /// </summary>
    /// <param name="userIds">The user identifiers to load.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
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
