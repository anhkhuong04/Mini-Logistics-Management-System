using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Operations;
using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Infrastructure.Persistence.Repositories;

public sealed class OperationsSettlementReadRepository(MiniLogisticsDbContext db) : IOperationsSettlementReadRepository
{
    public async Task<PagedResponse<OperationsCodCandidateResponse>> SearchAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var query = from cod in db.CodTransactions.AsNoTracking()
                    join shipment in db.Shipments.AsNoTracking() on cod.ShipmentId equals shipment.Id
                    join collector in db.Users.AsNoTracking() on cod.CollectedByUserId equals (Guid?)collector.Id into collectors
                    from collector in collectors.DefaultIfEmpty()
                    where cod.Status == CodStatus.Collected
                    orderby cod.CollectedAtUtc, cod.Id
                    select new OperationsCodCandidateResponse(shipment.Id, shipment.TrackingCode.Value,
                        shipment.ReceiverName, cod.Amount.Amount, cod.Amount.Currency, cod.CollectedAtUtc, cod.CollectedByUserId,
                        shipment.ReceiverPhone.Value, cod.Status, collector == null ? null : collector.FullName, collector == null ? null : collector.Email);
        var count = await query.CountAsync(cancellationToken);
        return new(await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken), pageNumber, pageSize, count);
    }
}
