using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GenerateShipmentLabel;

/// <summary>Renders an authorized shipment snapshot without changing the aggregate.</summary>
public interface IShipmentLabelRenderer
{
    byte[] Render(Shipment shipment);
}
