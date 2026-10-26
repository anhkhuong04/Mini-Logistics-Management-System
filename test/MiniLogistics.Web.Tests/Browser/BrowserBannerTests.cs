using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Banners;
using Xunit;
namespace MiniLogistics.Web.Tests.Browser;
public sealed class BrowserBannerTests
{
    [Fact]
    public async Task UploadedImageSurvivesHostRestartOutsideReleaseDirectory() {
        var root=Path.Combine(BrowserIdentityFactory.RepositoryRoot(),".agent","refactorfe-phase9","persistent-images");
        string url;
        await using(var first=new BrowserIdentityFactory(bannerRoot:root)) {
            await first.AddUser("persistent-banner@example.test","Admin"); using var client=first.Client();
            await BrowserShopShipmentReadTests.Login(client,"persistent-banner@example.test"); await BrowserIdentityFactory.Csrf(client);
            using var body=Upload(Png);var response=await client.PostAsync("/api/v1/app/admin/banners/images",body);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            url=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imageUrl").GetString()!;
        }
        await using var second=new BrowserIdentityFactory(bannerRoot:root);using var anonymous=second.Client();
        var image=await anonymous.GetAsync(url);Assert.Equal(HttpStatusCode.OK,image.StatusCode);Assert.Equal(Png,await image.Content.ReadAsByteArrayAsync());
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(root)!,"banner-persistence.json"),JsonSerializer.Serialize(new {root,url,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Png))}));
    }
    internal static readonly byte[] Png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jD1cAAAAASUVORK5CYII=");
    internal static MultipartFormDataContent Upload(byte[] bytes,string name="test.png",string mime="image/png") {
        var form=new MultipartFormDataContent(); var file=new ByteArrayContent(bytes); file.Headers.ContentType=new MediaTypeHeaderValue(mime);form.Add(file,"file",name);return form;
    }
    [Theory]
    [InlineData("Operator",true)]
    [InlineData("Shop",true)]
    [InlineData("Shipper",true)]
    [InlineData("IntegrationAdmin",true)]
    [InlineData("Admin",false)]
    public async Task NonAdminUseCasesCannotEnumerateMutateOrUploadEvenWhenCalledWithoutHttpGate(string role,bool active) {
        await using var factory=new BrowserIdentityFactory(); var actor=await factory.AddUser("banner-denied@example.test",role,active:active);
        using var scope=factory.Services.CreateScope(); var service=scope.ServiceProvider.GetRequiredService<IBannerService>();
        Assert.True((await service.GetAllBannersAsync(actor)).IsFailure);
        Assert.True((await service.CreateBannerAsync(actor,"title","/x.png",null,0,true)).IsFailure);
        Assert.True((await service.UpdateBannerAsync(actor,Guid.NewGuid(),"title","/x.png",null,0)).IsFailure);
        Assert.True((await service.ToggleBannerStatusAsync(actor,Guid.NewGuid())).IsFailure);
        Assert.True((await service.DeleteBannerAsync(actor,Guid.NewGuid())).IsFailure);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IUploadBannerImageService>().UploadAsync(actor,Stream.Null,"test.png","image/png")).IsFailure);
    }
    [Fact]
    public async Task AdminUploadLifecycleKeepsPublicActiveOnlyAndValidatesPayload() {
        await using var factory=new BrowserIdentityFactory(); using var client=factory.Client(); await factory.AddUser("banner-admin@example.test","Admin");
        await BrowserShopShipmentReadTests.Login(client,"banner-admin@example.test");
        using(var invalidCsrf=Upload(Png))Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/v1/app/admin/banners/images",invalidCsrf)).StatusCode);
        await BrowserIdentityFactory.Csrf(client);
        using(var invalid=Upload("invalid"u8.ToArray()))Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/v1/app/admin/banners/images",invalid)).StatusCode);
        using(var traversal=Upload(Png,"../test.png"))Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/v1/app/admin/banners/images",traversal)).StatusCode);
        using(var tooLarge=Upload(new byte[5*1024*1024+1]))Assert.Equal(HttpStatusCode.RequestEntityTooLarge,(await client.PostAsync("/api/v1/app/admin/banners/images",tooLarge)).StatusCode);
        using var multipart=Upload(Png);var uploaded=await client.PostAsync("/api/v1/app/admin/banners/images",multipart);Assert.Equal(HttpStatusCode.OK,uploaded.StatusCode);
        var url=(await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imageUrl").GetString()!;
        var file=await client.GetAsync(url);Assert.Equal(Png,await file.Content.ReadAsByteArrayAsync());Assert.Contains("sandbox",file.Headers.GetValues("Content-Security-Policy").Single());
        var body=new{title="Test banner",imageUrl=url,linkUrl="/tracking",sortOrder=1,isActive=true};
        var created=await client.PostAsJsonAsync("/api/v1/app/admin/banners",body);Assert.Equal(HttpStatusCode.OK,created.StatusCode);var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var anonymous=factory.Client();Assert.Single((await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/public/banners")).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/v1/app/admin/banners/{id}",body with {})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync($"/api/v1/app/admin/banners/{id}/toggle",new{})).StatusCode);
        Assert.Empty((await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/public/banners")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/v1/app/admin/banners/{id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/v1/app/admin/banners")).EnumerateArray());
    }
}
