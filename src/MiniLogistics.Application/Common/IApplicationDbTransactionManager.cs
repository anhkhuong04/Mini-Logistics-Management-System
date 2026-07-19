namespace MiniLogistics.Application.Common;

/// <summary>
/// Defines the application contract for Application Db Transaction.
/// </summary>
public interface IApplicationDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines lifecycle operations for Application Db Transaction Manager.
/// </summary>
public interface IApplicationDbTransactionManager
{
    Task<IApplicationDbTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);
}
