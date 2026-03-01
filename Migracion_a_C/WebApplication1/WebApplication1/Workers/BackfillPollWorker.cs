using IServices.IBackfillPoll;
using Microsoft.Extensions.Options;
using Models.WebApi;

namespace WebApplication1.Workers;

public class BackfillPollWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BackfillPollingOptions> options,
    ILogger<BackfillPollWorker> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly BackfillPollingOptions _options = options.Value;
    private readonly ILogger<BackfillPollWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.RunOnStartup)
        {
            await RunOnceSafeAsync("startup", stoppingToken);
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.WorkerIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);
            await RunOnceSafeAsync("scheduled", stoppingToken);
        }
    }

    private async Task RunOnceSafeAsync(string trigger, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IBackfillPollService>();

            await service.EjecutarAsync(new BackfillPollRunRequestDto
            {
                Trigger = trigger
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en BackfillPollWorker trigger={Trigger}", trigger);
        }
    }
}
