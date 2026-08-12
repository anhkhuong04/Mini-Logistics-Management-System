using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Banners;
using MiniLogistics.Domain.Banners;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class BannerRepository : IBannerRepository
{
    private readonly MiniLogisticsDbContext _dbContext;

    public BannerRepository(MiniLogisticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Banner>> GetActiveBannersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Banners
            .Where(b => b.IsActive)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Banner>> GetAllBannersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Banners
            .OrderBy(b => b.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<Banner?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Banners.FindAsync(new object[] { id }, cancellationToken);
    }

    public void Add(Banner banner)
    {
        _dbContext.Banners.Add(banner);
    }

    public void Remove(Banner banner)
    {
        _dbContext.Banners.Remove(banner);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
