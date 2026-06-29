using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

public interface IPartnerQuoteService
{
    Task<Result<PartnerShippingQuoteResponse>> QuoteAsync(
        PartnerQuoteCommand command,
        CancellationToken cancellationToken = default);
}
