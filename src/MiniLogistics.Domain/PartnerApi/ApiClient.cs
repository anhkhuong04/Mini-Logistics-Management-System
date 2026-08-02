using MiniLogistics.Domain.Common;
using System.Net;

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
        DateTimeOffset createdAtUtc,
        PartnerApiScope scopes = PartnerApiScope.All,
        IEnumerable<string>? allowedIpAddresses = null,
        DateTimeOffset? expiresAtUtc = null)
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
        ConfigureSecurity(scopes, allowedIpAddresses ?? [], expiresAtUtc, createdAtUtc);
        UpdatedAtUtc = null;
    }

    public Guid ShopId { get; private set; }

    public string Name { get; private set; }

    public string ApiKeyPrefix { get; private set; }

    public string ApiKeyHash { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public PartnerApiScope Scopes { get; private set; }

    public string? AllowedIpAddresses { get; private set; }

    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    public bool HasScope(PartnerApiScope scope) => (Scopes & scope) == scope;

    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= nowUtc;

    public bool IsIpAllowed(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(AllowedIpAddresses))
        {
            return true;
        }

        if (!IPAddress.TryParse(ipAddress, out var requestAddress))
        {
            return false;
        }

        var normalizedRequest = requestAddress.IsIPv4MappedToIPv6
            ? requestAddress.MapToIPv4()
            : requestAddress;
        return AllowedIpAddresses
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(IPAddress.Parse)
            .Select(address => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address)
            .Contains(normalizedRequest);
    }

    public void ConfigureSecurity(
        PartnerApiScope scopes,
        IEnumerable<string> allowedIpAddresses,
        DateTimeOffset? expiresAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        if (scopes == PartnerApiScope.None || (scopes & ~PartnerApiScope.All) != 0)
        {
            throw new DomainException("At least one valid partner API scope is required.");
        }

        if (expiresAtUtc.HasValue && expiresAtUtc.Value <= updatedAtUtc)
        {
            throw new DomainException("API client expiration must be in the future.");
        }

        Scopes = scopes;
        AllowedIpAddresses = NormalizeIpAddresses(allowedIpAddresses);
        ExpiresAtUtc = expiresAtUtc;
        MarkUpdated(updatedAtUtc);
    }

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

    private static string? NormalizeIpAddresses(IEnumerable<string> ipAddresses)
    {
        var normalized = new List<string>();
        foreach (var value in ipAddresses.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!IPAddress.TryParse(value.Trim(), out var address))
            {
                throw new DomainException($"Invalid IP whitelist entry: {value}.");
            }

            normalized.Add((address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString());
        }

        return normalized.Count == 0
            ? null
            : string.Join(',', normalized.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
    }

}
