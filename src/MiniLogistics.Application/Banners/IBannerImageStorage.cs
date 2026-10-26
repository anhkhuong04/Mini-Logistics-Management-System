using MiniLogistics.Domain.Common;
namespace MiniLogistics.Application.Banners;
public sealed record BannerImageResponse(string ImageUrl);
public interface IBannerImageStorage
{
    Task<Result<BannerImageResponse>> SaveAsync(Stream content,string fileName,string contentType,CancellationToken cancellationToken);
}
