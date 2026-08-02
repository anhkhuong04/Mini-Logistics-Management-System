using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.Shipments.ImportShipments;

namespace MiniLogistics.Infrastructure.Persistence;

public sealed class ShipmentImportWorker : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(3);
    private const int MaxRowsPerCycle = 20;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ShipmentImportWorker> _logger;

    public ShipmentImportWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ShipmentImportWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processedAny = false;
            for (var index = 0; index < MaxRowsPerCycle && !stoppingToken.IsCancellationRequested; index++)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<ShipmentImportBatchProcessor>();
                    if (!await processor.ProcessNextRowAsync(stoppingToken))
                    {
                        break;
                    }

                    processedAny = true;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Shipment import worker failed while processing a row.");
                    break;
                }
            }

            if (!processedAny)
            {
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }
}
