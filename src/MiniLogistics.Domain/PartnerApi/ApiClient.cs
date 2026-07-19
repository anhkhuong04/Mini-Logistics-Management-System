using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

/// <summary>
/// Represents the Api Client domain entity.
/// </summary>
public sealed class ApiClient : AuditableEntity
{
    private ApiClient()
    {
        Name = string.Empty;
        ApiKeyPrefix = string.Empty;
        ApiKeyHash = string.Empty;
    }

    public ApiClient(
        Guid shopId,
        string name,
        string apiKeyPrefix,
        string apiKeyHash,
        DateTimeOffset createdAtUtc)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (shopId == Guid.Empty)
        {
            throw new DomainException("Shop id is required.");
        }

        ShopId = shopId;
        Name = DomainGuard.RequireText(name, nameof(name), 150);
        ApiKeyPrefix = DomainGuard.RequireText(apiKeyPrefix, nameof(apiKeyPrefix), 32);
        ApiKeyHash = DomainGuard.RequireText(apiKeyHash, nameof(apiKeyHash), 128);
        IsActive = true;
    }

    public Guid ShopId { get; private set; }

    public string Name { get; private set; }

    public string ApiKeyPrefix { get; private set; }

    public string ApiKeyHash { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public void Rename(string name, DateTimeOffset updatedAtUtc)
    {
        Name = DomainGuard.RequireText(name, nameof(name), 150);
        MarkUpdated(updatedAtUtc);
    }

    public void RotateKey(string apiKeyPrefix, string apiKeyHash, DateTimeOffset updatedAtUtc)
    {
        ApiKeyPrefix = DomainGuard.RequireText(apiKeyPrefix, nameof(apiKeyPrefix), 32);
        ApiKeyHash = DomainGuard.RequireText(apiKeyHash, nameof(apiKeyHash), 128);
        MarkUpdated(updatedAtUtc);
    }

    public void MarkUsed(DateTimeOffset usedAtUtc)
    {
        LastUsedAtUtc = usedAtUtc;
        MarkUpdated(usedAtUtc);
    }

    public void Activate(DateTimeOffset updatedAtUtc)
    {
        IsActive = true;
        MarkUpdated(updatedAtUtc);
    }

    public void Deactivate(DateTimeOffset updatedAtUtc)
    {
        IsActive = false;
        MarkUpdated(updatedAtUtc);
    }

}
