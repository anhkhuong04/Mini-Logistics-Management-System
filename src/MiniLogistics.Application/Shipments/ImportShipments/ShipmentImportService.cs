using System.Globalization;
using System.Text;
using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Fees;
using MiniLogistics.Application.Routing;
using MiniLogistics.Application.Shops.ShopAccess;
using MiniLogistics.Application.Shipments.CreateShipment;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.ImportShipments;

public sealed class ShipmentImportService : IPreviewShipmentImportService, IConfirmShipmentImportService
{
    public const int MaxImportRows = 500;

    private const string DefaultCurrency = "VND";

    private static readonly string[] RequiredHeaders =
    [
        "clientOrderCode",
        "receiverName",
        "receiverPhone",
        "deliveryStreet",
        "deliveryWard",
        "deliveryProvince",
        "deliveryCountry",
        "weightKg",
        "lengthCm",
        "widthCm",
        "heightCm",
        "goodsValueAmount",
        "codAmount",
        "note"
    ];

    private readonly IValidator<PreviewShipmentImportCommand> _previewValidator;
    private readonly IValidator<ConfirmShipmentImportCommand> _confirmValidator;
    private readonly IValidator<CreateShipmentCommand> _createShipmentValidator;
    private readonly IShopAccessService _shopAccessService;
    private readonly IRouteClassificationService _routeClassificationService;
    private readonly IShippingFeeService _shippingFeeService;
    private readonly ICreateShipmentService _createShipmentService;
    private readonly IAdminAuditService _adminAuditService;

    public ShipmentImportService(
        IValidator<PreviewShipmentImportCommand> previewValidator,
        IValidator<ConfirmShipmentImportCommand> confirmValidator,
        IValidator<CreateShipmentCommand> createShipmentValidator,
        IShopAccessService shopAccessService,
        IRouteClassificationService routeClassificationService,
        IShippingFeeService shippingFeeService,
        ICreateShipmentService createShipmentService,
        IAdminAuditService? adminAuditService = null)
    {
        _previewValidator = previewValidator;
        _confirmValidator = confirmValidator;
        _createShipmentValidator = createShipmentValidator;
        _shopAccessService = shopAccessService;
        _routeClassificationService = routeClassificationService;
        _shippingFeeService = shippingFeeService;
        _createShipmentService = createShipmentService;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result<ShipmentImportPreviewResponse>> PreviewAsync(
        PreviewShipmentImportCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _previewValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<ShipmentImportPreviewResponse>.Failure(
                ApplicationErrors.ValidationFailed(ToValidationMessage(validationResult.Errors.Select(error => error.ErrorMessage))));
        }

        var shopResult = await _shopAccessService.GetShopForUserAsync(
            command.CurrentUserId,
            command.ShopId,
            requireActiveShop: true,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShipmentImportPreviewResponse>.Failure(shopResult.Error);
        }

        var csvResult = ParseCsv(command.CsvContent);
        if (csvResult.IsFailure)
        {
            return Result<ShipmentImportPreviewResponse>.Failure(csvResult.Error);
        }

        var csv = csvResult.Value;
        if (csv.Rows.Count > MaxImportRows)
        {
            return Result<ShipmentImportPreviewResponse>.Failure(
                ApplicationErrors.ValidationFailed($"CSV contains {csv.Rows.Count} rows. Maximum allowed is {MaxImportRows}."));
        }

        var missingHeaders = RequiredHeaders
            .Where(header => !csv.HeaderIndexes.ContainsKey(header))
            .ToList();
        if (missingHeaders.Count > 0)
        {
            return Result<ShipmentImportPreviewResponse>.Failure(
                ApplicationErrors.ValidationFailed($"Missing required CSV columns: {string.Join(", ", missingHeaders)}."));
        }

        var parsedRows = csv.Rows
            .Select(row => ParseDraft(row, csv.HeaderIndexes))
            .ToList();
        var duplicateRowNumbers = GetDuplicateClientOrderRowNumbers(parsedRows.Select(row => row.Draft));
        var previewRows = new List<ShipmentImportPreviewRowResponse>(parsedRows.Count);

        foreach (var row in parsedRows)
        {
            var errors = row.Errors.ToList();
            if (duplicateRowNumbers.Contains(row.Draft.RowNumber))
            {
                errors.Add("Duplicate clientOrderCode in CSV.");
            }

            var rowResult = await PreviewRowAsync(
                command.CurrentUserId,
                shopResult.Value,
                row.Draft,
                errors,
                cancellationToken);
            previewRows.Add(rowResult);
        }

        var response = new ShipmentImportPreviewResponse(
            shopResult.Value.Id,
            previewRows.Count,
            previewRows.Count(row => row.IsValid),
            previewRows.Count(row => !row.IsValid),
            previewRows);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.ShipmentImportPreviewed,
                AdminAuditTargetTypes.Shop,
                shopResult.Value.Id,
                NewValue: new
                {
                    response.TotalRows,
                    response.ValidRows,
                    response.InvalidRows
                }),
            cancellationToken);

        return Result<ShipmentImportPreviewResponse>.Success(response);
    }

    public async Task<Result<ShipmentImportConfirmResponse>> ConfirmAsync(
        ConfirmShipmentImportCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _confirmValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<ShipmentImportConfirmResponse>.Failure(
                ApplicationErrors.ValidationFailed(ToValidationMessage(validationResult.Errors.Select(error => error.ErrorMessage))));
        }

        var shopResult = await _shopAccessService.GetShopForUserAsync(
            command.CurrentUserId,
            command.ShopId,
            requireActiveShop: true,
            cancellationToken);
        if (shopResult.IsFailure)
        {
            return Result<ShipmentImportConfirmResponse>.Failure(shopResult.Error);
        }

        var duplicateRowNumbers = GetDuplicateClientOrderRowNumbers(command.Rows);
        var results = new List<ShipmentImportConfirmRowResponse>(command.Rows.Count);

        foreach (var row in command.Rows)
        {
            var rowErrors = duplicateRowNumbers.Contains(row.RowNumber)
                ? ["Duplicate clientOrderCode in CSV."]
                : new List<string>();

            var preview = await PreviewRowAsync(
                command.CurrentUserId,
                shopResult.Value,
                row,
                rowErrors,
                cancellationToken);

            if (!preview.IsValid)
            {
                results.Add(new ShipmentImportConfirmRowResponse(
                    row.RowNumber,
                    row.ClientOrderCode,
                    IsCreated: false,
                    ShipmentId: null,
                    TrackingCode: null,
                    preview.Errors));
                continue;
            }

            var createResult = await _createShipmentService.CreateAsync(
                BuildCreateShipmentCommand(command.CurrentUserId, shopResult.Value, row),
                cancellationToken);
            if (createResult.IsFailure)
            {
                results.Add(new ShipmentImportConfirmRowResponse(
                    row.RowNumber,
                    row.ClientOrderCode,
                    IsCreated: false,
                    ShipmentId: null,
                    TrackingCode: null,
                    [createResult.Error.Description]));
                continue;
            }

            results.Add(new ShipmentImportConfirmRowResponse(
                row.RowNumber,
                row.ClientOrderCode,
                IsCreated: true,
                createResult.Value.ShipmentId,
                createResult.Value.TrackingCode,
                []));
        }

        var response = new ShipmentImportConfirmResponse(
            results.Count,
            results.Count(row => row.IsCreated),
            results.Count(row => !row.IsCreated),
            results);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.CurrentUserId,
                AdminAuditActions.ShipmentImportConfirmed,
                AdminAuditTargetTypes.Shop,
                shopResult.Value.Id,
                NewValue: new
                {
                    response.TotalRows,
                    response.CreatedRows,
                    response.FailedRows
                }),
            cancellationToken);

        return Result<ShipmentImportConfirmResponse>.Success(response);
    }

    private async Task<ShipmentImportPreviewRowResponse> PreviewRowAsync(
        Guid currentUserId,
        Shop shop,
        ShipmentImportRowDraft row,
        IReadOnlyList<string> initialErrors,
        CancellationToken cancellationToken)
    {
        var errors = initialErrors.ToList();
        var createCommand = BuildCreateShipmentCommand(currentUserId, shop, row);
        var validationResult = await _createShipmentValidator.ValidateAsync(createCommand, cancellationToken);
        if (!validationResult.IsValid)
        {
            errors.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        if (errors.Count > 0)
        {
            return new ShipmentImportPreviewRowResponse(row, false, errors, null, null, null, DefaultCurrency);
        }

        var routeResult = _routeClassificationService.Classify(
            shop.Address.Province,
            row.DeliveryProvince);
        if (routeResult.IsFailure)
        {
            return new ShipmentImportPreviewRowResponse(
                row,
                false,
                [routeResult.Error.Description],
                null,
                null,
                null,
                DefaultCurrency);
        }

        try
        {
            var feeResult = await _shippingFeeService.CalculateAsync(
                routeResult.Value.RouteType,
                new Weight(row.WeightKg),
                new ParcelDimensions(row.LengthCm, row.WidthCm, row.HeightCm),
                new Money(row.GoodsValueAmount, DefaultCurrency),
                cancellationToken);
            if (feeResult.IsFailure)
            {
                return new ShipmentImportPreviewRowResponse(
                    row,
                    false,
                    [feeResult.Error.Description],
                    routeResult.Value.RouteType,
                    null,
                    null,
                    DefaultCurrency);
            }

            return new ShipmentImportPreviewRowResponse(
                row,
                true,
                [],
                routeResult.Value.RouteType,
                feeResult.Value.ChargeableWeightKg,
                feeResult.Value.TotalFee.Amount,
                feeResult.Value.TotalFee.Currency);
        }
        catch (DomainException exception)
        {
            return new ShipmentImportPreviewRowResponse(
                row,
                false,
                [exception.Message],
                routeResult.Value.RouteType,
                null,
                null,
                DefaultCurrency);
        }
    }

    private static CreateShipmentCommand BuildCreateShipmentCommand(
        Guid currentUserId,
        Shop shop,
        ShipmentImportRowDraft row)
    {
        return new CreateShipmentCommand(
            currentUserId,
            shop.Name,
            shop.PhoneNumber.Value,
            row.ReceiverName,
            row.ReceiverPhone,
            new ShipmentAddressDto(
                shop.Address.Street,
                shop.Address.Ward,
                shop.Address.Province,
                shop.Address.Country),
            new ShipmentAddressDto(
                row.DeliveryStreet,
                row.DeliveryWard,
                row.DeliveryProvince,
                string.IsNullOrWhiteSpace(row.DeliveryCountry) ? "Vietnam" : row.DeliveryCountry),
            row.WeightKg,
            row.LengthCm,
            row.WidthCm,
            row.HeightCm,
            row.GoodsValueAmount,
            row.CodAmount,
            DefaultCurrency,
            row.Note,
            shop.Id);
    }

    private static ParsedImportRow ParseDraft(CsvDataRow row, IReadOnlyDictionary<string, int> headerIndexes)
    {
        var errors = new List<string>();
        var draft = new ShipmentImportRowDraft(
            row.RowNumber,
            NormalizeOptional(GetField(row, headerIndexes, "clientOrderCode")),
            GetRequiredField(row, headerIndexes, "receiverName", errors),
            GetRequiredField(row, headerIndexes, "receiverPhone", errors),
            GetRequiredField(row, headerIndexes, "deliveryStreet", errors),
            GetRequiredField(row, headerIndexes, "deliveryWard", errors),
            GetRequiredField(row, headerIndexes, "deliveryProvince", errors),
            GetRequiredField(row, headerIndexes, "deliveryCountry", errors),
            GetRequiredDecimal(row, headerIndexes, "weightKg", errors),
            GetRequiredDecimal(row, headerIndexes, "lengthCm", errors),
            GetRequiredDecimal(row, headerIndexes, "widthCm", errors),
            GetRequiredDecimal(row, headerIndexes, "heightCm", errors),
            GetRequiredDecimal(row, headerIndexes, "goodsValueAmount", errors),
            GetRequiredDecimal(row, headerIndexes, "codAmount", errors),
            NormalizeOptional(GetField(row, headerIndexes, "note")));

        return new ParsedImportRow(draft, errors);
    }

    private static HashSet<int> GetDuplicateClientOrderRowNumbers(IEnumerable<ShipmentImportRowDraft> rows)
    {
        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.ClientOrderCode))
            .GroupBy(row => row.ClientOrderCode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(row => row.RowNumber))
            .ToHashSet();
    }

    private static string GetRequiredField(
        CsvDataRow row,
        IReadOnlyDictionary<string, int> headerIndexes,
        string header,
        List<string> errors)
    {
        var value = GetField(row, headerIndexes, header).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{header} is required.");
        }

        return value;
    }

    private static decimal GetRequiredDecimal(
        CsvDataRow row,
        IReadOnlyDictionary<string, int> headerIndexes,
        string header,
        List<string> errors)
    {
        var value = GetField(row, headerIndexes, header).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{header} is required.");
            return 0;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            errors.Add($"{header} must be a valid decimal number.");
            return 0;
        }

        return result;
    }

    private static string GetField(
        CsvDataRow row,
        IReadOnlyDictionary<string, int> headerIndexes,
        string header)
    {
        var index = headerIndexes[header];
        return index < row.Fields.Count ? row.Fields[index] : string.Empty;
    }

    private static string? NormalizeOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Result<CsvDocument> ParseCsv(string csvContent)
    {
        var rows = ReadCsvRows(csvContent)
            .Where(row => row.Any(field => !string.IsNullOrWhiteSpace(field)))
            .ToList();
        if (rows.Count == 0)
        {
            return Result<CsvDocument>.Failure(ApplicationErrors.ValidationFailed("CSV content is empty."));
        }

        var headerIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows[0].Count; index++)
        {
            var header = rows[0][index].Trim();
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            if (headerIndexes.ContainsKey(header))
            {
                return Result<CsvDocument>.Failure(ApplicationErrors.ValidationFailed($"Duplicate CSV column: {header}."));
            }

            headerIndexes[header] = index;
        }

        var dataRows = rows
            .Skip(1)
            .Select((fields, index) => new CsvDataRow(index + 2, fields))
            .ToList();
        if (dataRows.Count == 0)
        {
            return Result<CsvDocument>.Failure(ApplicationErrors.ValidationFailed("CSV does not contain data rows."));
        }

        return Result<CsvDocument>.Success(new CsvDocument(headerIndexes, dataRows));
    }

    private static IReadOnlyList<IReadOnlyList<string>> ReadCsvRows(string csvContent)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < csvContent.Length; index++)
        {
            var character = csvContent[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < csvContent.Length && csvContent[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    if (index + 1 < csvContent.Length && csvContent[index + 1] == '\n')
                    {
                        index++;
                    }

                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        row.Add(field.ToString());
        rows.Add(row);

        return rows;
    }

    private static string ToValidationMessage(IEnumerable<string> errors)
    {
        return string.Join("; ", errors.Where(error => !string.IsNullOrWhiteSpace(error)));
    }

    private sealed record CsvDocument(
        IReadOnlyDictionary<string, int> HeaderIndexes,
        IReadOnlyList<CsvDataRow> Rows);

    private sealed record CsvDataRow(
        int RowNumber,
        IReadOnlyList<string> Fields);

    private sealed record ParsedImportRow(
        ShipmentImportRowDraft Draft,
        IReadOnlyList<string> Errors);
}
