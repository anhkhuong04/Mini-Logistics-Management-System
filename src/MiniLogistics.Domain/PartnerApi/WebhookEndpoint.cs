using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.PartnerApi;

public sealed class WebhookEndpoint : AuditableEntity
{
    private WebhookEndpoint()
    {
        Url = string.Empty;
        SigningSecret = string.Empty;
    }

    public WebhookEndpoint(
        Guid apiClientId,
        string url,
        string signingSecret)
        : base(Guid.NewGuid())
    {
        if (apiClientId == Guid.Empty)
        {
            throw new DomainException("API client id is required.");
        }

        ApiClientId = apiClientId;
        Url = RequireUrl(url);
        SigningSecret = RequireText(signingSecret, nameof(signingSecret), 200);
        IsActive = true;
    }

    public Guid ApiClientId { get; private set; }

    public string Url { get; private set; }

    public string SigningSecret { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string url, string signingSecret)
    {
        Url = RequireUrl(url);
        SigningSecret = RequireText(signingSecret, nameof(signingSecret), 200);
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

    private static string RequireUrl(string value)
    {
        var trimmed = RequireText(value, nameof(value), 500);
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new DomainException("Webhook URL must be an absolute HTTP or HTTPS URL.");
        }

        return trimmed;
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
