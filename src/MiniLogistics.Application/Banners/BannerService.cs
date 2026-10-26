using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Banners;
using MiniLogistics.Domain.Common;
namespace MiniLogistics.Application.Banners;
public sealed class BannerService(IBannerRepository repository, IIdentityService identity) : IBannerService
{
    private static BannerDto Map(Banner b) => new(b.Id,b.Title,b.ImageUrl,b.LinkUrl,b.IsActive,b.SortOrder,b.CreatedAtUtc);
    public async Task<IReadOnlyList<BannerDto>> GetActiveBannersAsync(CancellationToken cancellationToken=default) =>
        (await repository.GetActiveBannersAsync(cancellationToken)).Select(Map).ToArray();
    public async Task<Result<IReadOnlyList<BannerDto>>> GetAllBannersAsync(Guid actorUserId,CancellationToken cancellationToken=default) {
        var access=await AdminUserAuthorization.EnsureActiveAdminAsync(identity,actorUserId,cancellationToken);
        return access.IsFailure?Result<IReadOnlyList<BannerDto>>.Failure(access.Error):Result<IReadOnlyList<BannerDto>>.Success((await repository.GetAllBannersAsync(cancellationToken)).Select(Map).ToArray());
    }
    private static bool SafeUrl(string? url) => !string.IsNullOrWhiteSpace(url) && url.Length<=1000 && !url.Any(char.IsControl)
        && (url.StartsWith('/')&&!url.StartsWith("//")&&!url.Contains('\\')&&!url.Contains("..")
            || Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo));
    private static bool Valid(string title,string image,string? link,int order) => !string.IsNullOrWhiteSpace(title)&&title.Length<=200&&SafeUrl(image)&&(string.IsNullOrWhiteSpace(link)||SafeUrl(link))&&order>=0;
    public async Task<Result<BannerDto>> CreateBannerAsync(Guid actorUserId,string title,string imageUrl,string? linkUrl,int sortOrder,bool isActive,CancellationToken cancellationToken=default) {
        var access=await AdminUserAuthorization.EnsureActiveAdminAsync(identity,actorUserId,cancellationToken); if(access.IsFailure)return Result<BannerDto>.Failure(access.Error);
        if(!Valid(title,imageUrl,linkUrl,sortOrder))return Result<BannerDto>.Failure(ApplicationErrors.ValidationFailed("Invalid banner."));
        var banner=new Banner(title.Trim(),imageUrl,linkUrl, isActive,sortOrder); repository.Add(banner); await repository.SaveChangesAsync(cancellationToken); return Result<BannerDto>.Success(Map(banner));
    }
    public async Task<Result<BannerDto>> UpdateBannerAsync(Guid actorUserId,Guid id,string title,string imageUrl,string? linkUrl,int sortOrder,CancellationToken cancellationToken=default) {
        var access=await AdminUserAuthorization.EnsureActiveAdminAsync(identity,actorUserId,cancellationToken); if(access.IsFailure)return Result<BannerDto>.Failure(access.Error);
        if(!Valid(title,imageUrl,linkUrl,sortOrder))return Result<BannerDto>.Failure(ApplicationErrors.ValidationFailed("Invalid banner."));
        var banner=await repository.GetByIdAsync(id,cancellationToken); if(banner is null)return Result<BannerDto>.Failure(ApplicationErrors.NotFound("Banner not found."));
        banner.Update(title.Trim(),imageUrl,linkUrl,sortOrder); await repository.SaveChangesAsync(cancellationToken); return Result<BannerDto>.Success(Map(banner));
    }
    public async Task<Result> ToggleBannerStatusAsync(Guid actorUserId,Guid id,CancellationToken cancellationToken=default) {
        var access=await AdminUserAuthorization.EnsureActiveAdminAsync(identity,actorUserId,cancellationToken); if(access.IsFailure)return access;
        var banner=await repository.GetByIdAsync(id,cancellationToken); if(banner is null)return Result.Failure(ApplicationErrors.NotFound("Banner not found."));
        banner.ToggleActiveStatus(); await repository.SaveChangesAsync(cancellationToken); return Result.Success();
    }
    public async Task<Result> DeleteBannerAsync(Guid actorUserId,Guid id,CancellationToken cancellationToken=default) {
        var access=await AdminUserAuthorization.EnsureActiveAdminAsync(identity,actorUserId,cancellationToken); if(access.IsFailure)return access;
        var banner=await repository.GetByIdAsync(id,cancellationToken); if(banner is not null){repository.Remove(banner);await repository.SaveChangesAsync(cancellationToken);} return Result.Success();
    }
}
