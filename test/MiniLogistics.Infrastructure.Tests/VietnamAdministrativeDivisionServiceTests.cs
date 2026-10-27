using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.ReferenceData;
using MiniLogistics.Infrastructure.ReferenceData;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class VietnamAdministrativeDivisionServiceTests
{
    [Fact]
    public async Task NormalizeProvinceWard_AcceptsCaseAndDiacriticVariants()
    {
        var service = new VietnamAdministrativeDivisionService(Path.Combine(FindWebRoot(), "data", "vietnam-administrative-divisions.json"));

        var result = await service.NormalizeProvinceWardAsync("ha noi", "hoan kiem");

        Assert.True(result.IsSuccess);
        Assert.Contains("Hà Nội", result.Value.Province);
        Assert.Contains("Hoàn Kiếm", result.Value.Ward);
    }

    [Fact]
    public async Task NormalizeProvinceWard_InvalidWardIsRejected()
    {
        var service = new VietnamAdministrativeDivisionService(Path.Combine(
            FindWebRoot(), "data", "vietnam-administrative-divisions.json"));

        var result = await service.NormalizeProvinceWardAsync("Hà Nội", "Ward does not exist");

        Assert.True(result.IsFailure);
        Assert.Equal("Application.ValidationFailed", result.Error.Code);
    }

    [Fact]
    public async Task RegistrationSharesAuthoritativeDataWithoutRequiringAWebHost()
    {
        var services = new ServiceCollection();
        services.AddAdministrativeDivisionReferenceData(Path.Combine(
            FindWebRoot(), "data", "vietnam-administrative-divisions.json"));
        await using var provider = services.BuildServiceProvider();
        var normalizer = provider.GetRequiredService<IAdministrativeDivisionService>();
        var catalog = provider.GetRequiredService<IAdministrativeDivisionCatalog>();
        Assert.Same(normalizer, catalog);
        Assert.NotEmpty(await catalog.GetProvincesAsync());
        Assert.True((await normalizer.NormalizeProvinceWardAsync("ha noi", "hoan kiem")).IsSuccess);
    }

    private static string FindWebRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "MiniLogistics.Web", "wwwroot");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate MiniLogistics.Web/wwwroot.");
    }
}
