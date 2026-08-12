using MiniLogistics.Domain.Banners;

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
    Task<IReadOnlyList<BannerDto>> GetAllBannersAsync(CancellationToken cancellationToken = default);
    Task<BannerDto> CreateBannerAsync(string title, string imageUrl, string? linkUrl, int sortOrder, bool isActive, CancellationToken cancellationToken = default);
    Task<BannerDto> UpdateBannerAsync(Guid id, string title, string imageUrl, string? linkUrl, int sortOrder, CancellationToken cancellationToken = default);
    Task ToggleBannerStatusAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteBannerAsync(Guid id, CancellationToken cancellationToken = default);
}
