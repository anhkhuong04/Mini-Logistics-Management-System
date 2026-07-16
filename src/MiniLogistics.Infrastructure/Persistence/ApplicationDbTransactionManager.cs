using Microsoft.EntityFrameworkCore.Storage;
using MiniLogistics.Application.Common;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class ApplicationDbTransactionManager : IApplicationDbTransactionManager
{
    private readonly MiniLogisticsDbContext _dbContext;

    public ApplicationDbTransactionManager(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IApplicationDbTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new ApplicationDbTransaction(transaction);
    }

    private sealed class ApplicationDbTransaction : IApplicationDbTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public ApplicationDbTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return _transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _transaction.DisposeAsync();
        }
    }
}
