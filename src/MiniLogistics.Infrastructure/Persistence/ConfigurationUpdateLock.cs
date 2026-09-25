using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniLogistics.Application.AdminSystemConfiguration;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class ConfigurationUpdateLock : IConfigurationUpdateLock
{
    private const int LockTimeoutMilliseconds = 5_000;

    private readonly MiniLogisticsDbContext _dbContext;

    public ConfigurationUpdateLock(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryAcquireAsync(
        string resource,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A configuration lock requires an active database transaction.");
        var connection = _dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = @timeout;
            SELECT @result;
            """;

        var resourceParameter = command.CreateParameter();
        resourceParameter.ParameterName = "@resource";
        resourceParameter.DbType = DbType.String;
        resourceParameter.Size = 255;
        resourceParameter.Value = resource;
        command.Parameters.Add(resourceParameter);

        var timeoutParameter = command.CreateParameter();
        timeoutParameter.ParameterName = "@timeout";
        timeoutParameter.DbType = DbType.Int32;
        timeoutParameter.Value = LockTimeoutMilliseconds;
        command.Parameters.Add(timeoutParameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null and not DBNull && Convert.ToInt32(result) >= 0;
    }
}
