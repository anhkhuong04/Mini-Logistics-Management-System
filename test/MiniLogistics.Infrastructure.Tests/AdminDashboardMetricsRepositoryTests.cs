using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.AdminDashboard;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class AdminDashboardMetricsRepositoryTests(LocalDbIntegrationFixture fixture)
    : IClassFixture<LocalDbIntegrationFixture>
{
    [Theory]
    [InlineData(null)]
    [InlineData("Ha Noi")]
    public async Task GetAsync_TranslatesCodDateAndProvinceFilters(string? province)
    {
        var fromUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var toUtc = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var result = await fixture.ExecuteAsync(provider =>
            provider.GetRequiredService<IAdminDashboardMetricsRepository>().GetAsync(
                fromUtc,
                toUtc,
                province,
                []));

        Assert.Equal("VND", result.Cod.Currency);
        Assert.Equal(0, result.Cod.PendingCollectionCount);
        Assert.Equal(0, result.Cod.CollectedAwaitingSettlementCount);
        Assert.Equal(0, result.Cod.SettledCount);
    }
}
