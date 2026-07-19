using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.PartnerApi;

namespace MiniLogistics.Application.PartnerApi;

public sealed class PartnerCredentialAuditWriter
{
    private readonly IPartnerApiCredentialAuditRepository _credentialAuditRepository;
    private readonly TimeProvider _timeProvider;

    public PartnerCredentialAuditWriter(
        IPartnerApiCredentialAuditRepository credentialAuditRepository,
        TimeProvider timeProvider)
    {
        _credentialAuditRepository = credentialAuditRepository;
        _timeProvider = timeProvider;
    }

    public async Task SaveAsync(
        Guid actorUserId,
        Guid shopId,
        Guid? apiClientId,
        string action,
        bool isSuccess,
        Error? error,
        CancellationToken cancellationToken)
    {
        await AddAsync(
            actorUserId,
            shopId,
            apiClientId,
            action,
            isSuccess,
            error,
            cancellationToken);
        await _credentialAuditRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(
        Guid actorUserId,
        Guid shopId,
        Guid? apiClientId,
        string action,
        bool isSuccess,
        Error? error,
        CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty || shopId == Guid.Empty)
        {
            return;
        }

        var audit = new PartnerApiCredentialAudit(
            actorUserId,
            shopId,
            apiClientId,
            action,
            isSuccess,
            _timeProvider.GetUtcNow(),
            errorCode: error?.Code,
            errorMessage: error?.Description);

        await _credentialAuditRepository.AddAsync(audit, cancellationToken);
    }
}
