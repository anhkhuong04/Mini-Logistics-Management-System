using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shippers.GetActiveShippers;

public sealed class GetActiveShippersService : IGetActiveShippersService
{
    private readonly IIdentityService _identityService;

    public GetActiveShippersService(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<Result<IReadOnlyList<GetActiveShipperResponse>>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var shippers = await _identityService.GetActiveShippersAsync(cancellationToken);
        var response = shippers
            .Select(shipper => new GetActiveShipperResponse(
                shipper.UserId,
                shipper.FullName,
                shipper.Email,
                shipper.PhoneNumber))
            .ToList();

        return Result<IReadOnlyList<GetActiveShipperResponse>>.Success(response);
    }
}
