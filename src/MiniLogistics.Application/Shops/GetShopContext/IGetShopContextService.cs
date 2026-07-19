using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shops.GetShopContext;

/// <summary>
/// Defines the application use case contract for Get Shop Context.
/// </summary>
public interface IGetShopContextService
{
    Task<Result<GetShopContextResponse>> GetAsync(
        Guid currentUserId,
        Guid? selectedShopId = null,
        CancellationToken cancellationToken = default);
}
