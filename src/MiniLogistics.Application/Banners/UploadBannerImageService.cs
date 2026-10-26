using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
namespace MiniLogistics.Application.Banners;
public interface IUploadBannerImageService
{
    Task<Result<BannerImageResponse>> UploadAsync(Guid actorUserId,Stream content,string fileName,string contentType,CancellationToken cancellationToken=default);
}
public sealed class UploadBannerImageService(IIdentityService identity,IBannerImageStorage storage) : IUploadBannerImageService
{
    public async Task<Result<BannerImageResponse>> UploadAsync(Guid actorUserId,Stream content,string fileName,string contentType,CancellationToken cancellationToken=default) {
        var access=await AdminUserAuthorization.EnsureActiveAdminAsync(identity,actorUserId,cancellationToken);
        return access.IsFailure?Result<BannerImageResponse>.Failure(access.Error):await storage.SaveAsync(content,fileName,contentType,cancellationToken);
    }
}
