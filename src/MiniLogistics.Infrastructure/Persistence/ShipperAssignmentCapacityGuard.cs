using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.AssignmentSelection;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class ShipperAssignmentCapacityGuard : IShipperAssignmentCapacityGuard
{
    private const int LockTimeoutMilliseconds = 5_000;

    private readonly MiniLogisticsDbContext _dbContext;
    private readonly IApplicationDbTransactionManager _transactionManager;
    private readonly IShipmentReadRepository _shipmentReadRepository;

    public ShipperAssignmentCapacityGuard(
        MiniLogisticsDbContext dbContext,
        IApplicationDbTransactionManager transactionManager,
        IShipmentReadRepository shipmentReadRepository)
    {
        _dbContext = dbContext;
        _transactionManager = transactionManager;
        _shipmentReadRepository = shipmentReadRepository;
    }

    public async Task<IShipperAssignmentCapacityLease?> TryAcquireAsync(
        Guid shipperId,
        CancellationToken cancellationToken = default)
    {
        if (shipperId == Guid.Empty)
        {
            return null;
        }

        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _transactionManager.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var maximumActiveShipments = await TryLockShipperRowAsync(shipperId, cancellationToken);
            if (maximumActiveShipments is null or < 1)
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }

                return null;
            }

            var counts = await _shipmentReadRepository.GetActiveAssignmentCountsByShipperIdsAsync(
                [shipperId],
                cancellationToken);
            counts.TryGetValue(shipperId, out var activeCount);
            if (activeCount >= maximumActiveShipments.Value)
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }

                return null;
            }

            return new TransactionLease(transaction);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }

            throw;
        }
    }

    private async Task<int?> TryLockShipperRowAsync(Guid shipperId, CancellationToken cancellationToken)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A database transaction is required to reserve shipper capacity.");
        var connection = _dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandTimeout = LockTimeoutMilliseconds / 1_000;
        command.CommandText = """
            SELECT [MaxActiveShipments]
            FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE [Id] = @shipperId;
            """;

        var shipperIdParameter = command.CreateParameter();
        shipperIdParameter.ParameterName = "@shipperId";
        shipperIdParameter.DbType = DbType.Guid;
        shipperIdParameter.Value = shipperId;
        command.Parameters.Add(shipperIdParameter);

        try
        {
            var maximumActiveShipments = await command.ExecuteScalarAsync(cancellationToken);
            return maximumActiveShipments is null or DBNull
                ? null
                : Convert.ToInt32(maximumActiveShipments);
        }
        catch (Microsoft.Data.SqlClient.SqlException exception)
            when (exception.Number is 1222 or -2)
        {
            return null;
        }
    }

    private sealed class TransactionLease(IApplicationDbTransaction? transaction) : IShipperAssignmentCapacityLease
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => transaction?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
