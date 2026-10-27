using MiniLogistics.Infrastructure.Storage.Banners;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace MiniLogistics.Infrastructure.Tests;

public sealed class BannerImageStorageTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(), "MiniLogisticsBannerTests", Guid.NewGuid().ToString("N"));
    private static LocalBannerImageStorage Storage(string root) => new(Options.Create(new BannerImageStorageOptions { RootPath = root }), NullLogger<LocalBannerImageStorage>.Instance);
    [Fact]
    public async Task UploadedBytesSurviveNewAdapter_AndOriginalFilenameCannotChoosePath()
    {
        var root = Root();
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jD1cAAAAASUVORK5CYII=");
        var result = await Storage(root).SaveAsync(new MemoryStream(bytes), "original.png", "image/png", default);
        Assert.True(result.IsSuccess);
        var name = Path.GetFileName(result.Value.ImageUrl);
        Assert.StartsWith("banner_", name);
        Assert.NotEqual("original.png", name);
        _ = Storage(root);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(root, name)));
        var traversal = await Storage(root).SaveAsync(new MemoryStream(bytes), "../escape.png", "image/png", default);
        Assert.True(traversal.IsFailure);
        Assert.Single(Directory.GetFiles(root));
    }
    [Theory]
    [InlineData("bad.svg", "image/svg+xml", "<svg xmlns='http://www.w3.org/2000/svg' onload='alert(1)'/> ")]
    [InlineData("bad.svg", "image/svg+xml", "<!DOCTYPE svg [<!ENTITY a SYSTEM 'file:///etc/passwd'>]><svg xmlns='http://www.w3.org/2000/svg'>&a;</svg>")]
    [InlineData("bad.svg", "image/svg+xml", "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>")]
    [InlineData("bad.svg", "image/svg+xml", "<?xml-stylesheet href='https://example.test/x'?><svg xmlns='http://www.w3.org/2000/svg'/>")]
    [InlineData("bad.svg", "image/svg+xml", "<svg xmlns='http://www.w3.org/2000/svg'><image href='https://example.test/x'/></svg>")]
    [InlineData("bad.png", "image/png", "<html>not an image</html>")]
    [InlineData("bad.exe", "image/png", "no")]
    [InlineData("bad.png", "image/jpeg", "no")]
    public async Task RejectsUntrustedTypesAndActiveContent(string name, string mime, string text)
    {
        var root = Root();
        var result = await Storage(root).SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes(text)), name, mime, default);
        Assert.True(result.IsFailure);
        Assert.False(Directory.Exists(root));
    }
    [Fact]
    public async Task SizeAndStorageFailures_AreSafeAndDoNotLeavePartialFiles()
    {
        var root = Root();
        Assert.True((await Storage(root).SaveAsync(new MemoryStream(new byte[LocalBannerImageStorage.MaxBytes + 1]), "large.png", "image/png", default)).IsFailure);
        Assert.False(Directory.Exists(root));
        var file = root + ".file";
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, "blocked");
        var image = Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1'><rect width='1' height='1'/></svg>");
        var failure = await Storage(file).SaveAsync(new MemoryStream(image), "valid.svg", "image/svg+xml", default);
        Assert.Equal("Application.Unavailable", failure.Error.Code);
        Assert.Equal("blocked", await File.ReadAllTextAsync(file));
    }
    [Theory]
    [InlineData(".svg", "image/svg+xml")]
    [InlineData(".gif", "image/gif")]
    [InlineData(".webp", "image/webp")]
    public async Task ExistingSafeFormatsRemainSupported(string extension, string type)
    {
        var bytes = extension switch
        {
            ".svg" => Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 10 10'><path d='M0 0h10v10H0z' fill='blue'/></svg>"),
            ".gif" => Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"),
            _ => Convert.FromBase64String("UklGRiIAAABXRUJQVlA4IBYAAAAwAQCdASoBAAEADsD+JaQAA3AAAAAA")
        };
        Assert.True((await Storage(Root()).SaveAsync(new MemoryStream(bytes), "safe" + extension, type, default)).IsSuccess);
    }
}
