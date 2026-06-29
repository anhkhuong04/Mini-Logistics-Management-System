using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public interface IPartnerApiRequestAuditRepository
{
    Task AddAsync(
        PartnerApiRequestAudit audit,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
