using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.CashOnDelivery.GetCodSettlementCandidates;

public sealed class GetCodSettlementCandidatesService : IGetCodSettlementCandidatesService
{
    private readonly ICodTransactionRepository _codTransactionRepository;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IIdentityService _identityService;

    public GetCodSettlementCandidatesService(
        ICodTransactionRepository codTransactionRepository,
        IShipmentRepository shipmentRepository,
        IIdentityService identityService)
    {
        _codTransactionRepository = codTransactionRepository;
        _shipmentRepository = shipmentRepository;
        _identityService = identityService;
    }

    public async Task<Result<IReadOnlyList<GetCodSettlementCandidateResponse>>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var codTransactions = await _codTransactionRepository.GetByStatusesAsync(
            [CodStatus.Collected],
            cancellationToken);

        if (codTransactions.Count == 0)
        {
            return Result<IReadOnlyList<GetCodSettlementCandidateResponse>>.Success([]);
        }

        var shipmentIds = codTransactions
            .Select(codTransaction => codTransaction.ShipmentId)
            .Distinct()
            .ToList();
        var shipments = await _shipmentRepository.GetByIdsAsync(shipmentIds, cancellationToken);
        var shipmentById = shipments.ToDictionary(shipment => shipment.Id);

        var collectedByUserIds = codTransactions
            .Select(codTransaction => codTransaction.CollectedByUserId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var users = await _identityService.GetUsersByIdsAsync(collectedByUserIds, cancellationToken);
        var userById = users.ToDictionary(user => user.UserId);

        var response = codTransactions
            .Where(codTransaction => shipmentById.ContainsKey(codTransaction.ShipmentId))
            .Select(codTransaction =>
            {
                var shipment = shipmentById[codTransaction.ShipmentId];
                IdentityUserSummaryResponse? collectedBy = null;
                if (codTransaction.CollectedByUserId.HasValue)
                {
                    userById.TryGetValue(codTransaction.CollectedByUserId.Value, out collectedBy);
                }

                return new GetCodSettlementCandidateResponse(
                    shipment.Id,
                    shipment.TrackingCode.Value,
                    shipment.ReceiverName,
                    shipment.ReceiverPhone.Value,
                    codTransaction.Amount.Amount,
                    codTransaction.Amount.Currency,
                    codTransaction.Status,
                    codTransaction.CollectedAtUtc,
                    codTransaction.CollectedByUserId,
                    collectedBy?.FullName,
                    collectedBy?.Email);
            })
            .ToList();

        return Result<IReadOnlyList<GetCodSettlementCandidateResponse>>.Success(response);
    }
}
