using FluentValidation;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Application.AdminHubs.GetAdminHubs;
using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Operations;
using MiniLogistics.Domain.Users;

namespace MiniLogistics.Application.AdminHubs.CreateHub;

public sealed class CreateHubService : ICreateHubService
{
    private readonly IValidator<CreateHubCommand> _validator;
    private readonly IIdentityService _identityService;
    private readonly IHubRepository _hubRepository;
    private readonly IAdminAuditService _adminAuditService;
    private readonly TimeProvider _timeProvider;

    public CreateHubService(
        IValidator<CreateHubCommand> validator,
        IIdentityService identityService,
        IHubRepository hubRepository,
        TimeProvider timeProvider,
        IAdminAuditService? adminAuditService = null)
    {
        _validator = validator;
        _identityService = identityService;
        _hubRepository = hubRepository;
        _timeProvider = timeProvider;
        _adminAuditService = adminAuditService ?? NullAdminAuditService.Instance;
    }

    public async Task<Result<AdminHubResponse>> CreateAsync(
        CreateHubCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var description = string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage));
            return Result<AdminHubResponse>.Failure(ApplicationErrors.ValidationFailed(description));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            command.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<AdminHubResponse>.Failure(authorizationResult.Error);
        }

        var existingHub = await _hubRepository.GetByCodeAsync(command.Code, cancellationToken);
        if (existingHub is not null)
        {
            return Result<AdminHubResponse>.Failure(ApplicationErrors.Conflict("Hub code already exists."));
        }

        var hub = new Hub(
            command.Code,
            command.Name,
            command.Province,
            _timeProvider.GetUtcNow(),
            command.Ward,
            command.AddressLine,
            command.IsRegionalSortingHub,
            command.Country);

        await _hubRepository.AddAsync(hub, cancellationToken);
        await _adminAuditService.RecordAsync(
            new AdminAuditEntry(
                command.RequestedByUserId,
                AdminAuditActions.HubCreated,
                AdminAuditTargetTypes.Hub,
                hub.Id,
                NewValue: ToAuditValue(hub),
                ActorRole: nameof(UserRole.Admin)),
            cancellationToken);
        await _hubRepository.SaveChangesAsync(cancellationToken);

        return Result<AdminHubResponse>.Success(GetAdminHubsService.ToResponse(hub, 0));
    }

    private static object ToAuditValue(Hub hub)
    {
        return new
        {
            hub.Code,
            hub.Name,
            hub.Province,
            hub.Ward,
            hub.AddressLine,
            hub.Country,
            hub.IsRegionalSortingHub,
            hub.IsActive
        };
    }
}
