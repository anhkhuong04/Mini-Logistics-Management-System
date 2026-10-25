using FluentValidation;
using MiniLogistics.Web.Services;
using MiniLogistics.Application.CashOnDelivery.MarkCodCollected;
using MiniLogistics.Application.CashOnDelivery.MarkCodSettled;
using MiniLogistics.Application.Operations;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Application.Shipments.UpdateShipmentStatus;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserOperationsStatusRequest(ShipmentStatus NewStatus, string? Note = null,
    FailureReasonCode? FailureReasonCode = null, GpsCoordinateDto? GpsCoordinate = null);
public sealed record BrowserCodCollectRequest(decimal? CollectedAmount = null, string? Note = null);
public sealed record BrowserCodSettleRequest(string? Note = null);
public static class BrowserOperationsActionEndpoints
{
    public static void MapBrowserOperationsActions(this RouteGroupBuilder group)
    {
        group.MapPost("/operations/shipments/{id:guid}/status", async (HttpContext context, Guid id,
            IValidator<UpdateShipmentStatusCommand> validator, IOperationsActionService service) => {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserOperationsStatusRequest>(context);
            var command = new UpdateShipmentStatusCommand(id, BrowserOperationsReadEndpoints.Actor(context), body.NewStatus, body.Note, body.FailureReasonCode, body.GpsCoordinate);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors)
                : BrowserOperationsAssignmentEndpoints.Empty(context, await service.StatusAsync(command, context.RequestAborted));
        }).WithName("BrowserOperationsStatus").Accepts<BrowserOperationsStatusRequest>("application/json").Produces(204);
        group.MapPost("/operations/shipments/{id:guid}/cod/collect", async (HttpContext context, Guid id,
            IValidator<MarkCodCollectedCommand> validator, IOperationsActionService service) => {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserCodCollectRequest>(context);
            var command = new MarkCodCollectedCommand(id, BrowserOperationsReadEndpoints.Actor(context), body.CollectedAmount, body.Note);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors)
                : BrowserOperationsAssignmentEndpoints.Empty(context, await service.CollectAsync(command, context.RequestAborted));
        }).WithName("BrowserOperationsCollect").Accepts<BrowserCodCollectRequest>("application/json").Produces(204);
        group.MapPost("/operations/shipments/{id:guid}/cod/settle", async (HttpContext context, Guid id,
            IValidator<MarkCodSettledCommand> validator, IOperationsActionService service) => {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserCodSettleRequest>(context);
            var command = new MarkCodSettledCommand(id, BrowserOperationsReadEndpoints.Actor(context), body.Note);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors)
                : BrowserOperationsAssignmentEndpoints.Empty(context, await service.SettleAsync(command, context.RequestAborted));
        }).WithName("BrowserOperationsSettle").Accepts<BrowserCodSettleRequest>("application/json").Produces(204);
    }
}
