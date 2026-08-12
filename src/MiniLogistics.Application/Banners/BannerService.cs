using MiniLogistics.Domain.Banners;

namespace MiniLogistics.Application.Banners;

public sealed class BannerService : IBannerService
{
    private readonly IBannerRepository _bannerRepository;

    public BannerService(IBannerRepository bannerRepository)
    {
        _bannerRepository = bannerRepository;
    }

    public async Task<IReadOnlyList<BannerDto>> GetActiveBannersAsync(CancellationToken cancellationToken = default)
    {
        var banners = await _bannerRepository.GetActiveBannersAsync(cancellationToken);
        return banners.Select(b => new BannerDto(
            b.Id,
            b.Title,
            b.ImageUrl,
            b.LinkUrl,
            b.IsActive,
            b.SortOrder,
            b.CreatedAtUtc)).ToList();
    }

    public async Task<IReadOnlyList<BannerDto>> GetAllBannersAsync(CancellationToken cancellationToken = default)
    {
        var banners = await _bannerRepository.GetAllBannersAsync(cancellationToken);
        return banners.Select(b => new BannerDto(
            b.Id,
            b.Title,
            b.ImageUrl,
            b.LinkUrl,
            b.IsActive,
            b.SortOrder,
            b.CreatedAtUtc)).ToList();
    }

    public async Task<BannerDto> CreateBannerAsync(string title, string imageUrl, string? linkUrl, int sortOrder, bool isActive, CancellationToken cancellationToken = default)
    {
        var banner = new Banner(title, imageUrl, linkUrl, isActive, sortOrder);
        
        _bannerRepository.Add(banner);
        await _bannerRepository.SaveChangesAsync(cancellationToken);

        return new BannerDto(
            banner.Id,
            banner.Title,
            banner.ImageUrl,
            banner.LinkUrl,
            banner.IsActive,
            banner.SortOrder,
            banner.CreatedAtUtc);
    }

    public async Task<BannerDto> UpdateBannerAsync(Guid id, string title, string imageUrl, string? linkUrl, int sortOrder, CancellationToken cancellationToken = default)
    {
        var banner = await _bannerRepository.GetByIdAsync(id, cancellationToken);
        if (banner == null)
        {
            throw new InvalidOperationException("Banner not found");
        }

        banner.Update(title, imageUrl, linkUrl, sortOrder);
        await _bannerRepository.SaveChangesAsync(cancellationToken);

        return new BannerDto(
            banner.Id,
            banner.Title,
            banner.ImageUrl,
            banner.LinkUrl,
            banner.IsActive,
            banner.SortOrder,
            banner.CreatedAtUtc);
    }

    public async Task ToggleBannerStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var banner = await _bannerRepository.GetByIdAsync(id, cancellationToken);
        if (banner == null)
        {
            throw new InvalidOperationException("Banner not found");
        }

        banner.ToggleActiveStatus();
        await _bannerRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBannerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var banner = await _bannerRepository.GetByIdAsync(id, cancellationToken);
        if (banner != null)
        {
            _bannerRepository.Remove(banner);
            await _bannerRepository.SaveChangesAsync(cancellationToken);
        }
    }
}
