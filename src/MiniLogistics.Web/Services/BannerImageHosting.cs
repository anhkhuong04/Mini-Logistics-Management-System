using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.StaticFiles;
using MiniLogistics.Infrastructure.Persistence.Storage;
namespace MiniLogistics.Web.Services;
public static class BannerImageHosting
{
    public static void UseBannerImages(this WebApplication app) {
        var options=app.Services.GetRequiredService<IOptions<BannerImageStorageOptions>>().Value;
        if(!Path.IsPathFullyQualified(options.RootPath))throw new InvalidOperationException("BannerImages:RootPath must be absolute.");
        var root=Path.GetFullPath(options.RootPath);
        if(root.Equals(Path.GetFullPath(app.Environment.ContentRootPath),StringComparison.OrdinalIgnoreCase)
            ||root.StartsWith(Path.GetFullPath(app.Environment.ContentRootPath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Banner image storage must be outside the release content root.");
        Directory.CreateDirectory(root);
        void Headers(StaticFileResponseContext c) { c.Context.Response.Headers.XContentTypeOptions="nosniff"; c.Context.Response.Headers.ContentSecurityPolicy="default-src 'none'; sandbox; style-src 'unsafe-inline'"; }
        app.UseStaticFiles(new StaticFileOptions { RequestPath="/images/banners",FileProvider=new PhysicalFileProvider(root),OnPrepareResponse=Headers });
        // Existing releases may still contain legacy banner assets; keep those URLs readable.
        var legacy=Path.Combine(app.Environment.WebRootPath??Path.Combine(app.Environment.ContentRootPath,"wwwroot"),"images","banners");
        if(Directory.Exists(legacy))app.UseStaticFiles(new StaticFileOptions{RequestPath="/images/banners",FileProvider=new PhysicalFileProvider(legacy),OnPrepareResponse=Headers});
    }
}
