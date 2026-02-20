using IServices.IJornada;
using Microsoft.Extensions.Options;
using Models.WebApi;

namespace WebApplication1.Workers;

public class JornadaStatusWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<JornadaProcessingOptions> options,
    ILogger<JornadaStatusWorker> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly JornadaProcessingOptions _options = options.Value;
    private readonly ILogger<JornadaStatusWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_options.WorkerIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var jornadaService = scope.ServiceProvider.GetRequiredService<IJornadaService>();
                var updated = jornadaService.MarcarIncompletasVencidasComoError(DateTimeOffset.UtcNow);

                if (updated > 0)
                {
                    _logger.LogInformation("JornadaStatusWorker marco {Count} jornadas como ERROR", updated);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en JornadaStatusWorker");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
