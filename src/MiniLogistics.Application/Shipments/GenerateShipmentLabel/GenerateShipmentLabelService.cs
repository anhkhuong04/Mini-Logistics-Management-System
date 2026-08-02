using System.Text;
using System.Globalization;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;

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
        var shopResult = await _shopAccessService.GetShopAccessAsync(
            command.OwnerUserId,
            command.ShopId,
            requireActiveShop: false,
            ShopPermission.ViewShipments | ShopPermission.ViewFullPii,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShipmentLabelResponse>.Failure(shopResult.Error);
        }

        var shipment = await _shipmentRepository.GetByIdAndShopIdAsync(
            command.ShipmentId,
            shopResult.Value.Shop.Id,
            cancellationToken);
        if (shipment is null)
        {
            return Result<ShipmentLabelResponse>.Failure(ApplicationErrors.NotFound("Shipment was not found for current shop."));
        }

        if (shipment.Status == ShipmentStatus.Draft)
        {
            return Result<ShipmentLabelResponse>.Failure(ApplicationErrors.ValidationFailed("Draft shipment does not have a shipping label."));
        }

        var pdf = LabelPdfBuilder.Create(shipment);

        return Result<ShipmentLabelResponse>.Success(new ShipmentLabelResponse(
            $"label-{shipment.TrackingCode.Value}.pdf",
            "application/pdf",
            pdf));
    }

    private static class LabelPdfBuilder
    {
        private static readonly IReadOnlyDictionary<char, string> Code39Patterns = new Dictionary<char, string>
        {
            ['0'] = "nnnwwnwnn",
            ['1'] = "wnnwnnnnw",
            ['2'] = "nnwwnnnnw",
            ['3'] = "wnwwnnnnn",
            ['4'] = "nnnwwnnnw",
            ['5'] = "wnnwwnnnn",
            ['6'] = "nnwwwnnnn",
            ['7'] = "nnnwnnwnw",
            ['8'] = "wnnwnnwnn",
            ['9'] = "nnwwnnwnn",
            ['A'] = "wnnnnwnnw",
            ['B'] = "nnwnnwnnw",
            ['C'] = "wnwnnwnnn",
            ['D'] = "nnnnwwnnw",
            ['E'] = "wnnnwwnnn",
            ['F'] = "nnwnwwnnn",
            ['G'] = "nnnnnwwnw",
            ['H'] = "wnnnnwwnn",
            ['I'] = "nnwnnwwnn",
            ['J'] = "nnnnwwwnn",
            ['K'] = "wnnnnnwnw",
            ['L'] = "nnwnnnwnw",
            ['M'] = "wnwnnnwnn",
            ['N'] = "nnnnwnwnw",
            ['O'] = "wnnnwnwnn",
            ['P'] = "nnwnwnwnn",
            ['Q'] = "nnnnnnwww",
            ['R'] = "wnnnnnwwn",
            ['S'] = "nnwnnnwwn",
            ['T'] = "nnnnwnwwn",
            ['U'] = "wwnnnnnnw",
            ['V'] = "nwwnnnnnw",
            ['W'] = "wwwnnnnnn",
            ['X'] = "nwnnwnnnw",
            ['Y'] = "wwnnwnnnn",
            ['Z'] = "nwwnwnnnn",
            ['-'] = "nwnnnnwnw",
            ['.'] = "wwnnnnwnn",
            [' '] = "nwwnnnwnn",
            ['$'] = "nwnwnwnnn",
            ['/'] = "nwnwnnnwn",
            ['+'] = "nwnnnwnwn",
            ['%'] = "nnnwnwnwn",
            ['*'] = "nwnnwnwnn"
        };

        public static byte[] Create(Shipment shipment)
        {
            var content = new StringBuilder();
            DrawRect(content, 32, 32, 531, 778, stroke: true, fill: false);
            DrawText(content, "MINILOGISTICS SHIPPING LABEL", 48, 780, 16, bold: true);
            DrawText(content, shipment.TrackingCode.Value, 48, 748, 24, bold: true);
            DrawText(content, $"CODE39: *{NormalizeCode39Value(shipment.TrackingCode.Value)}*", 48, 724, 9, bold: false);
            DrawCode39(content, shipment.TrackingCode.Value, 48, 666, 48);

            DrawSection(content, "RECEIVER", 48, 612, 238, 132);
            DrawText(content, shipment.ReceiverName, 60, 584, 13, bold: true);
            DrawText(content, shipment.ReceiverPhone.Value, 60, 562, 11, bold: false);
            DrawWrappedText(content, shipment.DeliveryAddress.FullAddress, 60, 540, 210, 11, maxLines: 3);

            DrawSection(content, "SENDER", 310, 612, 205, 132);
            DrawText(content, shipment.SenderName, 322, 584, 13, bold: true);
            DrawText(content, shipment.SenderPhone.Value, 322, 562, 11, bold: false);
            DrawWrappedText(content, shipment.PickupAddress.FullAddress, 322, 540, 180, 11, maxLines: 3);

            DrawSection(content, "ROUTE / SERVICE", 48, 446, 238, 136);
            DrawText(content, $"Route: {shipment.RouteType}", 60, 418, 12, bold: true);
            DrawText(content, $"Pickup province: {shipment.PickupAddress.Province}", 60, 394, 10, bold: false);
            DrawText(content, $"Delivery province: {shipment.DeliveryAddress.Province}", 60, 374, 10, bold: false);
            DrawText(content, $"Status: {shipment.Status}", 60, 354, 10, bold: false);

            DrawSection(content, "PAYMENT / PARCEL", 310, 446, 205, 136);
            DrawText(content, $"COD: {shipment.CodAmount.Amount.ToString("N0", CultureInfo.InvariantCulture)} {shipment.CodAmount.Currency}", 322, 418, 12, bold: true);
            DrawText(content, $"Shipping fee: {shipment.ShippingFee.Amount.ToString("N0", CultureInfo.InvariantCulture)} {shipment.ShippingFee.Currency}", 322, 394, 10, bold: false);
            DrawText(content, $"Weight: {shipment.Weight.Kilograms.ToString("N3", CultureInfo.InvariantCulture)} kg", 322, 374, 10, bold: false);
            DrawText(content, $"Chargeable: {shipment.ChargeableWeight.Kilograms.ToString("N3", CultureInfo.InvariantCulture)} kg", 322, 354, 10, bold: false);
            DrawText(content, $"Size: {shipment.ParcelDimensions.LengthCm:N0} x {shipment.ParcelDimensions.WidthCm:N0} x {shipment.ParcelDimensions.HeightCm:N0} cm", 322, 334, 10, bold: false);

            DrawSection(content, "NOTE", 48, 280, 467, 94);
            DrawWrappedText(content, string.IsNullOrWhiteSpace(shipment.Note) ? "-" : shipment.Note!, 60, 252, 430, 11, maxLines: 3);

            DrawText(content, "Shop copy", 48, 82, 9, bold: false);
            DrawText(content, "Do not hand over parcel without successful pickup scan.", 48, 64, 9, bold: false);

            var stream = Encoding.ASCII.GetBytes(content.ToString());
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
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

        private static void DrawSection(
            StringBuilder content,
            string title,
            decimal x,
            decimal y,
            decimal width,
            decimal height)
        {
            DrawRect(content, x, y - height, width, height, stroke: true, fill: false);
            DrawRect(content, x, y - 24, width, 24, stroke: false, fill: true, gray: 0.94m);
            DrawText(content, title, x + 10, y - 16, 10, bold: true);
        }

        private static void DrawText(
            StringBuilder content,
            string value,
            decimal x,
            decimal y,
            int size,
            bool bold)
        {
            content.AppendLine("BT");
            content.Append('/').Append(bold ? "F2" : "F1").Append(' ').Append(size.ToString(CultureInfo.InvariantCulture)).AppendLine(" Tf");
            content.Append(Format(x)).Append(' ').Append(Format(y)).AppendLine(" Td");
            content.Append('(').Append(Escape(value)).AppendLine(") Tj");
            content.AppendLine("ET");
        }

        private static void DrawWrappedText(
            StringBuilder content,
            string value,
            decimal x,
            decimal y,
            decimal width,
            int size,
            int maxLines)
        {
            var maxCharactersPerLine = Math.Max(12, (int)(width / (size * 0.55m)));
            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var lines = new List<string>();
            var current = new StringBuilder();
            foreach (var word in words)
            {
                if (current.Length + word.Length + 1 > maxCharactersPerLine && current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(word);
            }

            if (current.Length > 0)
            {
                lines.Add(current.ToString());
            }

            for (var index = 0; index < Math.Min(maxLines, lines.Count); index++)
            {
                var line = index == maxLines - 1 && lines.Count > maxLines
                    ? lines[index] + "..."
                    : lines[index];
                DrawText(content, line, x, y - index * (size + 5), size, bold: false);
            }
        }

        private static void DrawRect(
            StringBuilder content,
            decimal x,
            decimal y,
            decimal width,
            decimal height,
            bool stroke,
            bool fill,
            decimal gray = 0m)
        {
            if (fill)
            {
                content.Append(Format(gray)).AppendLine(" g");
            }

            content
                .Append(Format(x)).Append(' ')
                .Append(Format(y)).Append(' ')
                .Append(Format(width)).Append(' ')
                .Append(Format(height)).AppendLine(" re");
            content.AppendLine(stroke && fill ? "B" : stroke ? "S" : "f");
            content.AppendLine("0 g");
        }

        private static void DrawCode39(
            StringBuilder content,
            string value,
            decimal x,
            decimal y,
            decimal height)
        {
            const decimal narrow = 1.25m;
            const decimal wide = 3.75m;
            var cursor = x;
            var encoded = "*" + NormalizeCode39Value(value) + "*";

            foreach (var character in encoded)
            {
                var pattern = Code39Patterns[character];
                for (var index = 0; index < pattern.Length; index++)
                {
                    var barWidth = pattern[index] == 'w' ? wide : narrow;
                    if (index % 2 == 0)
                    {
                        DrawRect(content, cursor, y, barWidth, height, stroke: false, fill: true);
                    }

                    cursor += barWidth;
                }

                cursor += narrow;
            }
        }

        private static string NormalizeCode39Value(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in value.ToUpperInvariant())
            {
                if (Code39Patterns.ContainsKey(character) && character != '*')
                {
                    builder.Append(character);
                }
            }

            return builder.Length == 0 ? "UNKNOWN" : builder.ToString();
        }

        private static string Format(decimal value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
