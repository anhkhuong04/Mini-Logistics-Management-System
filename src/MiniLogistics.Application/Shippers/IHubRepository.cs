using MiniLogistics.Domain.Operations;

namespace MiniLogistics.Application.Shippers;

public interface IHubRepository
{
    Task<IReadOnlyList<Hub>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Hub>> GetByIdsAsync(
        IReadOnlyCollection<Guid> hubIds,
        CancellationToken cancellationToken = default);

    Task<Hub?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Hub hub,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
