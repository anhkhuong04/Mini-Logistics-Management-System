using MiniLogistics.Application.AdminUsers;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Identity;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.AdminCod;

public sealed class GetAdminCodReportService : IGetAdminCodReportService
{
    private readonly IIdentityService _identityService;
    private readonly IAdminCodReportRepository _reportRepository;

    public GetAdminCodReportService(
        IIdentityService identityService,
        IAdminCodReportRepository reportRepository)
    {
        _identityService = identityService;
        _reportRepository = reportRepository;
    }

    public async Task<Result<AdminCodReportResponse>> GetAsync(
        AdminCodReportQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.RequestedByUserId == Guid.Empty)
        {
            return Result<AdminCodReportResponse>.Failure(
                ApplicationErrors.ValidationFailed("Requested by user id is required."));
        }

        if (query.MinAmount.HasValue && query.MaxAmount.HasValue && query.MaxAmount < query.MinAmount)
        {
            return Result<AdminCodReportResponse>.Failure(
                ApplicationErrors.ValidationFailed("Maximum amount must be greater than or equal to minimum amount."));
        }

        if (query.FromUtc.HasValue && query.ToUtc.HasValue && query.ToUtc < query.FromUtc)
        {
            return Result<AdminCodReportResponse>.Failure(
                ApplicationErrors.ValidationFailed("To date must be greater than or equal to from date."));
        }

        var authorizationResult = await AdminUserAuthorization.EnsureActiveAdminAsync(
            _identityService,
            query.RequestedByUserId,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<AdminCodReportResponse>.Failure(authorizationResult.Error);
        }

        var report = await _reportRepository.GetAsync(query, cancellationToken);
        return Result<AdminCodReportResponse>.Success(report);
    }
}
