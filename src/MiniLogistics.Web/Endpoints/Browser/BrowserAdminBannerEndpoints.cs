using System.Text.Json;
using MiniLogistics.Application.Banners;
namespace MiniLogistics.Web.Endpoints.Browser;
public sealed record BrowserBannerRequest(string Title,string ImageUrl,string? LinkUrl,int SortOrder,bool IsActive=true);
public sealed record BrowserBannerUploadForm(IFormFile File);
public static class BrowserAdminBannerEndpoints
{
    public static void MapBrowserAdminBanners(this RouteGroupBuilder group) {
        group.MapGet("/admin/banners",async(HttpContext c,IBannerService s)=>BrowserApiResultMapper.From(c,await s.GetAllBannersAsync(BrowserAdminReportingEndpoints.Actor(c),c.RequestAborted)))
            .WithName("BrowserAdminBanners").Produces<IReadOnlyList<BannerDto>>();
        group.MapPost("/admin/banners",async(HttpContext c,IBannerService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserBannerRequest>(c); return BrowserApiResultMapper.From(c,await s.CreateBannerAsync(BrowserAdminReportingEndpoints.Actor(c),b.Title,b.ImageUrl,b.LinkUrl,b.SortOrder,b.IsActive,c.RequestAborted));
        }).WithName("BrowserCreateBanner").Accepts<BrowserBannerRequest>("application/json").Produces<BannerDto>();
        group.MapPut("/admin/banners/{id:guid}",async(HttpContext c,Guid id,IBannerService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserBannerRequest>(c); return BrowserApiResultMapper.From(c,await s.UpdateBannerAsync(BrowserAdminReportingEndpoints.Actor(c),id,b.Title,b.ImageUrl,b.LinkUrl,b.SortOrder,c.RequestAborted));
        }).WithName("BrowserUpdateBanner").Accepts<BrowserBannerRequest>("application/json").Produces<BannerDto>();
        group.MapPost("/admin/banners/{id:guid}/toggle",async(HttpContext c,Guid id,IBannerService s)=>BrowserOperationsAssignmentEndpoints.Empty(c,await s.ToggleBannerStatusAsync(BrowserAdminReportingEndpoints.Actor(c),id,c.RequestAborted)))
            .WithName("BrowserToggleBanner").Produces(204);
        group.MapDelete("/admin/banners/{id:guid}",async(HttpContext c,Guid id,IBannerService s)=>BrowserOperationsAssignmentEndpoints.Empty(c,await s.DeleteBannerAsync(BrowserAdminReportingEndpoints.Actor(c),id,c.RequestAborted)))
            .WithName("BrowserDeleteBanner").Produces(204);
        group.MapPost("/admin/banners/images",async(HttpContext c,IUploadBannerImageService s)=>{
            if(!c.User.IsInRole("Admin"))return BrowserApiResultMapper.Problem(c,"Application.Forbidden",403);
            if(!c.Request.HasFormContentType)throw new BadHttpRequestException("Expected multipart.");
            var form=await c.Request.ReadFormAsync(c.RequestAborted); if(form.Files.Count!=1)throw new JsonException();
            var file=form.Files[0]; if(file.Length>5*1024*1024)return BrowserApiResultMapper.Problem(c,"Request.TooLarge",413);
            await using var stream=file.OpenReadStream(); return BrowserApiResultMapper.From(c,await s.UploadAsync(BrowserAdminReportingEndpoints.Actor(c),stream,file.FileName,file.ContentType,c.RequestAborted));
        }).Accepts<BrowserBannerUploadForm>("multipart/form-data").WithMetadata(new BrowserRequestBodyLimit(5*1024*1024+64*1024)).WithName("BrowserUploadBannerImage").Produces<BannerImageResponse>();
    }
    public static void MapBrowserPublicBanners(this IEndpointRouteBuilder endpoints) => endpoints.MapGet("/api/v1/public/banners",async(HttpContext c,IBannerService s,CancellationToken token)=>{
        c.Response.Headers.CacheControl="no-store";return BrowserApiResultMapper.Json(await s.GetActiveBannersAsync(token));
    }).AllowAnonymous().WithGroupName("browser-v1").WithName("BrowserActiveBanners").Produces<IReadOnlyList<BannerDto>>();
}
