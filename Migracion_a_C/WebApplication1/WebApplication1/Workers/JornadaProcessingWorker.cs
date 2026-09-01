using IServices.IJornada;
using Microsoft.Extensions.Options;
using Models.WebApi;

namespace WebApplication1.Workers;

public class JornadaProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<JornadaProcessingOptions> options,
    ILogger<JornadaProcessingWorker> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly JornadaProcessingOptions _options = options.Value;
    private readonly ILogger<JornadaProcessingWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idleDelay = TimeSpan.FromSeconds(Math.Max(1, _options.WorkerIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                for (var index = 0; index < Math.Max(1, _options.BatchSize); index++)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IJornadaProjectionService>();
                    if (!service.ProcessNext(DateTimeOffset.UtcNow))
                    {
                        break;
                    }

                    processed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no recuperable en JornadaProcessingWorker");
            }

            if (processed == 0)
            {
                await Task.Delay(idleDelay, stoppingToken);
            }
            else
            {
                await Task.Yield();
            }
        }
    }
}
