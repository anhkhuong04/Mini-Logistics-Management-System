using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MiniLogistics.Infrastructure.PartnerApi;

public sealed class PartnerApiRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly PartnerApiRetentionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PartnerApiRetentionWorker> _logger;

    public PartnerApiRetentionWorker(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<PartnerApiRetentionOptions> options,
        TimeProvider timeProvider,
        ILogger<PartnerApiRetentionWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (!_options.RunOnStartup)
        {
            await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<PartnerApiRetentionService>();
                var deleted = await service.DeleteExpiredAsync(
                    _timeProvider.GetUtcNow(),
                    stoppingToken);
                _logger.LogInformation(
                    "Partner API retention completed and removed {DeletedRowCount} rows.",
                    deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Partner API retention failed.");
            }

            await Task.Delay(TimeSpan.FromHours(_options.IntervalHours), stoppingToken);
        }
    }
}
