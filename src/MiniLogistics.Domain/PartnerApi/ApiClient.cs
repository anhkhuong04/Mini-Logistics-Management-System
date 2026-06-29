using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

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
        string apiKeyHash)
        : base(Guid.NewGuid())
    {
        if (shopId == Guid.Empty)
        {
            throw new DomainException("Shop id is required.");
        }

        ShopId = shopId;
        Name = RequireText(name, nameof(name), 150);
        ApiKeyPrefix = RequireText(apiKeyPrefix, nameof(apiKeyPrefix), 32);
        ApiKeyHash = RequireText(apiKeyHash, nameof(apiKeyHash), 128);
        IsActive = true;
    }

    public Guid ShopId { get; private set; }

    public string Name { get; private set; }

    public string ApiKeyPrefix { get; private set; }

    public string ApiKeyHash { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public void Rename(string name)
    {
        Name = RequireText(name, nameof(name), 150);
        MarkUpdated();
    }

    public void RotateKey(string apiKeyPrefix, string apiKeyHash)
    {
        ApiKeyPrefix = RequireText(apiKeyPrefix, nameof(apiKeyPrefix), 32);
        ApiKeyHash = RequireText(apiKeyHash, nameof(apiKeyHash), 128);
        MarkUpdated();
    }

    public void MarkUsed()
    {
        LastUsedAtUtc = DateTimeOffset.UtcNow;
        MarkUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    private static string RequireText(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }
}
