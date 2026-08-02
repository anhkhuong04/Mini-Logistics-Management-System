using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class VietnamAdministrativeDivisionServiceTests
{
    [Fact]
    public async Task NormalizeProvinceWard_AcceptsCaseAndDiacriticVariants()
    {
        var service = new VietnamAdministrativeDivisionService(new TestWebHostEnvironment(FindWebRoot()));

        var result = await service.NormalizeProvinceWardAsync("ha noi", "hoan kiem");

        Assert.True(result.IsSuccess);
        Assert.Contains("Hà Nội", result.Value.Province);
        Assert.Contains("Hoàn Kiếm", result.Value.Ward);
    }

    [Fact]
    public async Task NormalizeProvinceWard_InvalidWardIsRejected()
    {
        var service = new VietnamAdministrativeDivisionService(new TestWebHostEnvironment(FindWebRoot()));

        var result = await service.NormalizeProvinceWardAsync("Hà Nội", "Ward does not exist");

        Assert.True(result.IsFailure);
        Assert.Equal("Application.ValidationFailed", result.Error.Code);
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

    private sealed class TestWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "MiniLogistics.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(webRootPath);
        public string WebRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Directory.GetParent(webRootPath)!.FullName;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(Directory.GetParent(webRootPath)!.FullName);
    }
}
