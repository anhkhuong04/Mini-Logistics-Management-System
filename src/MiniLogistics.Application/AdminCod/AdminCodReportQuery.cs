using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Application.AdminCod;

public sealed record AdminCodReportQuery(
    Guid RequestedByUserId,
    CodStatus? Status = null,
    Guid? ShipperId = null,
    string? Province = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null);
