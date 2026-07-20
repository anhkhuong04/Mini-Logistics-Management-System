using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.Authorization;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Users;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Application.Shipments.ProofOfDelivery;

public sealed class SubmitDeliveryProofService : ISubmitDeliveryProofService
{
    private readonly IValidator<SubmitDeliveryProofCommand> _validator;
    private readonly IShipmentReadRepository _shipmentRepository;
    private readonly IDeliveryProofRepository _proofRepository;
    private readonly IIdentityService _identityService;
    private readonly IOperationAuthorizationService _operationAuthorizationService;
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;

    public SubmitDeliveryProofService(
        IValidator<SubmitDeliveryProofCommand> validator,
        IShipmentReadRepository shipmentRepository,
        IDeliveryProofRepository proofRepository,
        IIdentityService identityService,
        TimeProvider timeProvider,
        IOperationAuthorizationService? operationAuthorizationService = null,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _shipmentRepository = shipmentRepository;
        _proofRepository = proofRepository;
        _identityService = identityService;
        _timeProvider = timeProvider;
        _operationAuthorizationService = operationAuthorizationService ?? new OperationAuthorizationService(identityService);
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result<DeliveryProofResponse>> SubmitAsync(
        SubmitDeliveryProofCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<DeliveryProofResponse>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var shipment = await _shipmentRepository.GetTrackedByIdAsync(command.ShipmentId, cancellationToken);
        if (shipment is null)
        {
            return Result<DeliveryProofResponse>.Failure(ApplicationErrors.NotFound("Shipment was not found."));
        }

        var authorizationResult = await EnsureCanSubmitAsync(command.SubmittedByUserId, shipment, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<DeliveryProofResponse>.Failure(authorizationResult.Error);
        }

        try
        {
            var now = _timeProvider.GetUtcNow();
            var gpsCoordinate = command.GpsCoordinate is null
                ? null
                : new GpsCoordinate(
                    command.GpsCoordinate.Latitude,
                    command.GpsCoordinate.Longitude,
                    command.GpsCoordinate.AccuracyMeters,
                    command.GpsCoordinate.CapturedAtUtc);
            var proof = new DeliveryProof(
                command.ShipmentId,
                command.ProofType,
                command.ProofMethod,
                command.ResourceUri,
                command.RecipientName,
                command.VerificationText,
                gpsCoordinate,
                command.SubmittedByUserId,
                command.CapturedAtUtc ?? now,
                now);

            await _proofRepository.AddAsync(proof, cancellationToken);
            await _adminAuditService.RecordAsync(
                new AdminAuditEntry(
                    command.SubmittedByUserId,
                    AdminAuditActions.ShipmentPodUploaded,
                    AdminAuditTargetTypes.Shipment,
                    shipment.Id,
                    NewValue: new
                    {
                        ProofId = proof.Id,
                        proof.ProofType,
                        proof.ProofMethod,
                        proof.ResourceUri,
                        HasGps = proof.Latitude.HasValue && proof.Longitude.HasValue
                    }),
                cancellationToken);
            await _proofRepository.SaveChangesAsync(cancellationToken);

            return Result<DeliveryProofResponse>.Success(ToResponse(proof));
        }
        catch (DomainException exception)
        {
            return Result<DeliveryProofResponse>.Failure(ApplicationErrors.ValidationFailed(exception.Message));
        }
    }

    private async Task<Result> EnsureCanSubmitAsync(
        Guid actorUserId,
        Shipment shipment,
        CancellationToken cancellationToken)
    {
        var operationPermission = await _operationAuthorizationService.EnsurePermissionAsync(
            actorUserId,
            OperationPermissions.ShipmentProofSubmit,
            "Proof submit user was not found.",
            "Proof submit user is not active.",
            "Only Admin, Operator or assigned Shipper can submit shipment proof.",
            cancellationToken);
        if (operationPermission.IsSuccess)
        {
            return Result.Success();
        }

        var shipperCheck = await _identityService.CheckUserRoleAsync(
            actorUserId,
            nameof(UserRole.Shipper),
            cancellationToken);
        if (!shipperCheck.Exists)
        {
            return Result.Failure(ApplicationErrors.NotFound("Proof submit user was not found."));
        }

        if (!shipperCheck.IsActive)
        {
            return Result.Failure(ApplicationErrors.Forbidden("Proof submit user is not active."));
        }

        if (!shipperCheck.IsInRole)
        {
            return Result.Failure(ApplicationErrors.Forbidden(
                "Only Admin, Operator or assigned Shipper can submit shipment proof."));
        }

        return shipment.Assignments.Any(assignment =>
            assignment.IsActive && assignment.ShipperId == actorUserId)
            ? Result.Success()
            : Result.Failure(ApplicationErrors.Forbidden(
                "Shipper can only submit proof for shipments assigned to them."));
    }

    private static DeliveryProofResponse ToResponse(DeliveryProof proof)
    {
        return new DeliveryProofResponse(
            proof.Id,
            proof.ShipmentId,
            proof.ProofType,
            proof.ProofMethod,
            proof.ResourceUri,
            proof.RecipientName,
            proof.VerificationText,
            proof.Latitude,
            proof.Longitude,
            proof.GpsAccuracyMeters,
            proof.GpsCapturedAtUtc,
            proof.SubmittedByUserId,
            proof.CapturedAtUtc,
            proof.SubmittedAtUtc);
    }
}
