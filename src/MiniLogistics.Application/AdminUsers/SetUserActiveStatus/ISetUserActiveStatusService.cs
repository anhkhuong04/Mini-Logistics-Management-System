using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.SetUserActiveStatus;

/// <summary>
/// Defines the application use case contract for Set User Active Status.
/// </summary>
public interface ISetUserActiveStatusService
{
    Task<Result> SetAsync(
        SetUserActiveStatusCommand command,
        CancellationToken cancellationToken = default);
}
