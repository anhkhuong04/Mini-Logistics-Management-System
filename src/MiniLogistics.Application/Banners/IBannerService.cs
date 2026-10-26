using MiniLogistics.Domain.Banners;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Banners;

public record BannerDto(
    Guid Id,
    string Title,
    string ImageUrl,
    string? LinkUrl,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAtUtc);

public interface IBannerService
{
    Task<IReadOnlyList<BannerDto>> GetActiveBannersAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BannerDto>>> GetAllBannersAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task<Result<BannerDto>> CreateBannerAsync(Guid actorUserId, string title, string imageUrl, string? linkUrl, int sortOrder, bool isActive, CancellationToken cancellationToken = default);
    Task<Result<BannerDto>> UpdateBannerAsync(Guid actorUserId, Guid id, string title, string imageUrl, string? linkUrl, int sortOrder, CancellationToken cancellationToken = default);
    Task<Result> ToggleBannerStatusAsync(Guid actorUserId, Guid id, CancellationToken cancellationToken = default);
    Task<Result> DeleteBannerAsync(Guid actorUserId, Guid id, CancellationToken cancellationToken = default);
}
