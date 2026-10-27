using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.CashOnDelivery.MarkCodSettled;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Operations;

public interface IOperationsActionService
{
    Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(Guid actor, Guid shipmentId, CancellationToken token);
    Task<Result> StatusAsync(UpdateShipmentStatusCommand command, CancellationToken token);
    Task<Result> CollectAsync(MarkCodCollectedCommand command, CancellationToken token);
    Task<Result> SettleAsync(MarkCodSettledCommand command, CancellationToken token);
}
