using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.SetUserActiveStatus;

public interface ISetUserActiveStatusService
{
    Task<Result> SetAsync(
        SetUserActiveStatusCommand command,
        CancellationToken cancellationToken = default);
}
