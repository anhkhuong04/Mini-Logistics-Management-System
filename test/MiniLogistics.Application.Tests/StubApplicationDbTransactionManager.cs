using MiniLogistics.Application.Common;

namespace MiniLogistics.Application.Tests;

internal sealed class StubApplicationDbTransactionManager : IApplicationDbTransactionManager
{
    public Task<IApplicationDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IApplicationDbTransaction>(new Transaction());
    }

    private sealed class Transaction : IApplicationDbTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
