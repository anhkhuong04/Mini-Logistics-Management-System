using MiniLogistics.Domain.Banners;

namespace MiniLogistics.Application.Banners;

public interface IBannerRepository
{
    Task<IReadOnlyList<Banner>> GetActiveBannersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Banner>> GetAllBannersAsync(CancellationToken cancellationToken = default);
    Task<Banner?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Banner banner);
    void Remove(Banner banner);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
