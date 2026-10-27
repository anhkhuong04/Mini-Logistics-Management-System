using System.Text.Json;
using FluentValidation;
using MiniLogistics.Web.Services;
using MiniLogistics.Application.Operations;
using MiniLogistics.Application.Shipments.AssignShipperToShipment;
using MiniLogistics.Application.Shipments.AutoAssignShipment;
using MiniLogistics.Application.Shipments.BulkRetryAutoAssignment;
using MiniLogistics.Application.Shipments.CancelShipmentAssignment;
using MiniLogistics.Application.Shipments.ReassignShipment;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserAssignmentRequest(Guid ShipperId, string? Note = null);
public sealed record BrowserReassignmentRequest(Guid ShipperId, string Reason);
public sealed record BrowserAssignmentCancelRequest(string Reason);
public sealed record BrowserBulkRetryRequest(IReadOnlyList<Guid> ShipmentIds);

public static class BrowserOperationsAssignmentEndpoints
{
    public static void MapBrowserOperationsAssignments(this RouteGroupBuilder group)
    {
        group.MapPost("/operations/shipments/{id:guid}/assign", async (HttpContext context, Guid id,
            IValidator<AssignShipperCommand> validator, IAssignShipperToShipmentService service) =>
        {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserAssignmentRequest>(context);
            var command = new AssignShipperCommand(id, body.ShipperId, BrowserUserClaims.GetUserId(context), body.Note);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors) : BrowserApiResultMapper.Empty(context, await service.AssignAsync(command, context.RequestAborted));
        }).WithName("BrowserOperationsAssign").Accepts<BrowserAssignmentRequest>("application/json").Produces(204);
        group.MapPost("/operations/shipments/{id:guid}/auto-assign", async (HttpContext context, Guid id, IOperationsActionService service) =>
        {
            if (id == Guid.Empty)
                throw new JsonException();
            return BrowserApiResultMapper.From(context, await service.AutoAssignAsync(BrowserUserClaims.GetUserId(context), id, context.RequestAborted));
        }).WithName("BrowserOperationsAutoAssign").Produces<AutoAssignShipmentResult>();
        group.MapPost("/operations/shipments/{id:guid}/reassign", async (HttpContext context, Guid id,
            IValidator<ReassignShipmentCommand> validator, IReassignShipmentService service) =>
        {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserReassignmentRequest>(context);
            var command = new ReassignShipmentCommand(id, body.ShipperId, BrowserUserClaims.GetUserId(context), body.Reason);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors) : BrowserApiResultMapper.Empty(context, await service.ReassignAsync(command, context.RequestAborted));
        }).WithName("BrowserOperationsReassign").Accepts<BrowserReassignmentRequest>("application/json").Produces(204);
        group.MapPost("/operations/shipments/{id:guid}/cancel-assignment", async (HttpContext context, Guid id,
            IValidator<CancelShipmentAssignmentCommand> validator, ICancelShipmentAssignmentService service) =>
        {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserAssignmentCancelRequest>(context);
            var command = new CancelShipmentAssignmentCommand(id, BrowserUserClaims.GetUserId(context), body.Reason);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors) : BrowserApiResultMapper.Empty(context, await service.CancelAsync(command, context.RequestAborted));
        }).WithName("BrowserOperationsCancelAssignment").Accepts<BrowserAssignmentCancelRequest>("application/json").Produces(204);
        group.MapPost("/operations/assignments/bulk-retry", async (HttpContext context,
            IValidator<BulkRetryAutoAssignmentCommand> validator, IBulkRetryAutoAssignmentService service) =>
        {
            var body = await BrowserApiEndpoints.ReadAsync<BrowserBulkRetryRequest>(context);
            if (body.ShipmentIds is null || body.ShipmentIds.Contains(Guid.Empty))
                throw new JsonException();
            var command = new BulkRetryAutoAssignmentCommand(BrowserUserClaims.GetUserId(context), body.ShipmentIds);
            var validation = await validator.ValidateAsync(command, context.RequestAborted);
            return !validation.IsValid ? BrowserValidationProblem.From(context, validation.Errors)
                : BrowserApiResultMapper.From(context, await service.RetryAsync(command, context.RequestAborted));
        }).WithName("BrowserOperationsBulkRetry").Accepts<BrowserBulkRetryRequest>("application/json").Produces<BulkRetryAutoAssignmentResult>();
    }
}
