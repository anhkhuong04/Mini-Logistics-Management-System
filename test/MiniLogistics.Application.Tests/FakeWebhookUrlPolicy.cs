using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Tests;

internal sealed class FakeWebhookUrlPolicy : IWebhookUrlPolicy
{
    public Task<Result> ValidateAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }
}
