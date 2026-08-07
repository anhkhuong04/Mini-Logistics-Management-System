using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Validates webhook destinations at registration time and immediately before delivery.
/// </summary>
public interface IWebhookUrlPolicy
{
    Task<Result> ValidateAsync(
        string url,
        CancellationToken cancellationToken = default);
}
