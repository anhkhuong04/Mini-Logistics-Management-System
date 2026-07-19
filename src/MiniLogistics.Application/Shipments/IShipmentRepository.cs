namespace MiniLogistics.Application.Shipments;

/// <summary>
/// Combines shipment read and write operations for services that need a full shipment unit of work.
/// </summary>
public interface IShipmentRepository : IShipmentReadRepository, IShipmentWriteRepository
{
}
