using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.PartnerApi;

/// <summary>
/// Calculates shipping quotes for authenticated Partner API clients.
/// </summary>
public interface IPartnerQuoteService
{
    /// <summary>
    /// Calculates a fee quote without creating a shipment.
    /// </summary>
    /// <param name="command">The authenticated quote request including shop, parcel, and route data.</param>
    /// <param name="cancellationToken">A token used to cancel the asynchronous operation.</param>
    Task<Result<PartnerShippingQuoteResponse>> QuoteAsync(
        PartnerQuoteCommand command,
        CancellationToken cancellationToken = default);
}
