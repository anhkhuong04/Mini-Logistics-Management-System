using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

/// <summary>
/// Represents the Webhook Endpoint domain entity.
/// </summary>
public sealed class WebhookEndpoint : AuditableEntity
{
    private WebhookEndpoint()
    {
        Url = string.Empty;
        ProtectedSigningSecret = string.Empty;
    }

    public WebhookEndpoint(
        Guid apiClientId,
        string url,
        string protectedSigningSecret,
        DateTimeOffset createdAtUtc)
        : base(Guid.NewGuid(), createdAtUtc)
    {
        if (apiClientId == Guid.Empty)
        {
            throw new DomainException("API client id is required.");
        }

        ApiClientId = apiClientId;
        Url = RequireUrl(url);
        ProtectedSigningSecret = DomainGuard.RequireText(protectedSigningSecret, nameof(protectedSigningSecret), 2048);
        SecretVersion = 1;
        IsActive = true;
    }

    public Guid ApiClientId { get; private set; }

    public string Url { get; private set; }

    public string ProtectedSigningSecret { get; private set; }

    public int SecretVersion { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string url, string protectedSigningSecret, DateTimeOffset updatedAtUtc)
    {
        Url = RequireUrl(url);
        ProtectedSigningSecret = DomainGuard.RequireText(protectedSigningSecret, nameof(protectedSigningSecret), 2048);
        SecretVersion++;
        MarkUpdated(updatedAtUtc);
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

    private static string RequireUrl(string value)
    {
        var trimmed = DomainGuard.RequireText(value, nameof(value), 500);
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new DomainException("Webhook URL must be an absolute HTTPS URL.");
        }

        return trimmed;
    }

}
