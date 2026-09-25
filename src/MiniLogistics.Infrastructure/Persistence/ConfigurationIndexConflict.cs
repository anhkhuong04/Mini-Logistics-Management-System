using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Common;

namespace MiniLogistics.Infrastructure.Persistence;

internal static class ConfigurationIndexConflict
{
    private static readonly string[] IndexNames =
    [
        "IX_RouteRegionConfigs_Province_Version",
        "UX_RouteRegionConfigs_Province_Active",
        "IX_FeeRules_RouteType_Version",
        "UX_FeeRules_RouteType_Active"
    ];

    public static bool IsConflict(DbUpdateException exception)
    {
        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
            {
                continue;
            }

            foreach (SqlError error in sqlException.Errors)
            {
                if (error.Number is not (2601 or 2627))
                {
                    continue;
                }

                if (IndexNames.Any(indexName => error.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static ConcurrencyConflictException ToException(DbUpdateException exception) => new(
        "A configuration version or active configuration already exists for this scope.",
        exception);
}
