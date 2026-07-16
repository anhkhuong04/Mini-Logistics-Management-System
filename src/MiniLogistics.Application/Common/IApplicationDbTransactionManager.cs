namespace MiniLogistics.Application.Common;

public interface IApplicationDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationDbTransactionManager
{
    Task<IApplicationDbTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);
}
