using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminUsers.ResetUserPassword;

public interface IResetUserPasswordService
{
    Task<Result> ResetAsync(
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken = default);
}
