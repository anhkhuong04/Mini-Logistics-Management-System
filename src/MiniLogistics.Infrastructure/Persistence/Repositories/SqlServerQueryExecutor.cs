using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

internal static class SqlServerQueryExecutor
{
    public static async Task<IReadOnlyList<Guid>> QueryIdsAsync(
        MiniLogisticsDbContext dbContext,
        string sql,
        IReadOnlyCollection<SqlQueryParameter> parameters,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var parameter in parameters)
            {
                AddParameter(command, parameter);
            }

            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ids.Add(reader.GetGuid(0));
            }

            return ids;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, SqlQueryParameter value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = value.Name;
        parameter.Value = value.Value;
        parameter.DbType = value.DbType;
        if (value.Size.HasValue)
        {
            parameter.Size = value.Size.Value;
        }

        command.Parameters.Add(parameter);
    }
}

internal readonly record struct SqlQueryParameter(
    string Name,
    object Value,
    DbType DbType,
    int? Size = null);
