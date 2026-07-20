using System.Text;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GenerateShipmentLabel;

public sealed class GenerateShipmentLabelService : IGenerateShipmentLabelService
{
    private readonly IShopAccessService _shopAccessService;
    private readonly IShipmentReadRepository _shipmentRepository;

    public GenerateShipmentLabelService(
        IShopAccessService shopAccessService,
        IShipmentReadRepository shipmentRepository)
    {
        _shopAccessService = shopAccessService;
        _shipmentRepository = shipmentRepository;
    }

    public async Task<Result<ShipmentLabelResponse>> GenerateAsync(
        GenerateShipmentLabelCommand command,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await _shopAccessService.GetShopForUserAsync(
            command.OwnerUserId,
            command.ShopId,
            requireActiveShop: false,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShipmentLabelResponse>.Failure(shopResult.Error);
        }

        var shipment = await _shipmentRepository.GetByIdAndShopIdAsync(
            command.ShipmentId,
            shopResult.Value.Id,
            cancellationToken);
        if (shipment is null)
        {
            return Result<ShipmentLabelResponse>.Failure(ApplicationErrors.NotFound("Shipment was not found for current shop."));
        }

        if (shipment.Status == ShipmentStatus.Draft)
        {
            return Result<ShipmentLabelResponse>.Failure(ApplicationErrors.ValidationFailed("Draft shipment does not have a shipping label."));
        }

        var pdf = MinimalPdfBuilder.CreateSinglePage([
            "MINILOGISTICS SHIPPING LABEL",
            $"Tracking: {shipment.TrackingCode.Value}",
            $"Receiver: {shipment.ReceiverName} - {shipment.ReceiverPhone.Value}",
            $"Delivery: {shipment.DeliveryAddress.FullAddress}",
            $"Sender: {shipment.SenderName} - {shipment.SenderPhone.Value}",
            $"Pickup: {shipment.PickupAddress.FullAddress}",
            $"Route: {shipment.RouteType}",
            $"COD: {shipment.CodAmount.Amount:N0} {shipment.CodAmount.Currency}",
            $"Weight: {shipment.Weight.Kilograms:N3} kg | Chargeable: {shipment.ChargeableWeight.Kilograms:N3} kg",
            $"Note: {shipment.Note ?? "-"}"
        ]);

        return Result<ShipmentLabelResponse>.Success(new ShipmentLabelResponse(
            $"label-{shipment.TrackingCode.Value}.pdf",
            "application/pdf",
            pdf));
    }

    private static class MinimalPdfBuilder
    {
        public static byte[] CreateSinglePage(IReadOnlyList<string> lines)
        {
            var content = new StringBuilder();
            content.AppendLine("BT");
            content.AppendLine("/F1 16 Tf");
            content.AppendLine("50 780 Td");
            foreach (var line in lines)
            {
                content.Append('(').Append(Escape(line)).AppendLine(") Tj");
                content.AppendLine("0 -28 Td");
            }

            content.AppendLine("ET");
            var stream = Encoding.ASCII.GetBytes(content.ToString());
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                $"<< /Length {stream.Length} >>\nstream\n{content}endstream"
            };

            using var output = new MemoryStream();
            WriteAscii(output, "%PDF-1.4\n");
            var offsets = new List<long> { 0 };
            for (var index = 0; index < objects.Count; index++)
            {
                offsets.Add(output.Position);
                WriteAscii(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
            }

            var xrefOffset = output.Position;
            WriteAscii(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1))
            {
                WriteAscii(output, $"{offset:0000000000} 00000 n \n");
            }

            WriteAscii(output, $"trailer << /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
            return output.ToArray();
        }

        private static string Escape(string value)
        {
            var ascii = new string(value.Select(character => character <= 127 ? character : '?').ToArray());
            return ascii.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }

        private static void WriteAscii(Stream stream, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
