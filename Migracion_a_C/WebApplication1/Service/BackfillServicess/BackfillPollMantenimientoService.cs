using System.Text.Json;
using Dominio;
using IDataAcces;
using IServices.IAccesEvent;
using IServices.IBackfillPoll;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.WebApi;

namespace Service.BackfillServicess;

public class BackfillPollMantenimientoService(
    IRelojesRepository relojesRepository,
    IBackfillPollRunsRepository pollRunsRepository,
    IAccesEventService accesEventService,
    IHikvisionAcsEventClient hikvisionClient,
    IOptions<BackfillPollingOptions> options,
    ILogger<BackfillPollMantenimientoService> logger) : IBackfillPollMantenimientoService
{
    private readonly IRelojesRepository _relojesRepository = relojesRepository;
    private readonly IBackfillPollRunsRepository _pollRunsRepository = pollRunsRepository;
    private readonly IAccesEventService _accesEventService = accesEventService;
    private readonly IHikvisionAcsEventClient _hikvisionClient = hikvisionClient;
    private readonly BackfillPollingOptions _options = options.Value;
    private readonly ILogger<BackfillPollMantenimientoService> _logger = logger;

    private static readonly SemaphoreSlim RunLock = new(1, 1);
    private static readonly object StatusSync = new();
    private static BackfillPollStatusDto _status = new();

    public BackfillPollStatusDto ObtenerEstado()
    {
        lock (StatusSync)
        {
            if (!_status.IsRunning && string.IsNullOrWhiteSpace(_status.LastRunId))
            {
                HydrateFromLastPersistedRun();
            }

            return CloneStatus(_status);
        }
    }

    public async Task<BackfillPollRunResultDto> EjecutarAsync(BackfillPollRunRequestDto request, CancellationToken ct)
    {
        if (!await RunLock.WaitAsync(0, ct))
        {
            throw new InvalidOperationException("Ya existe una corrida de poll en ejecucion");
        }

        var run = new BackfillPollRunResultDto
        {
            RunId = Guid.NewGuid().ToString("N"),
            Trigger = request.Trigger,
            StartedAtUtc = DateTimeOffset.UtcNow,
            Status = "running"
        };

        try
        {
            SetRunningStatus(run);
            PersistStartedRun(run);

            var nowUtc = DateTimeOffset.UtcNow;
            var relojes = _relojesRepository.GetPollCandidates(request.ResidentialId, request.RelojId);
            run.TotalClocks = relojes.Count;

            foreach (var reloj in relojes)
            {
                ct.ThrowIfCancellationRequested();

                BackfillPollClockResultDto clockResult;
                try
                {
                    clockResult = await ProcessClockAsync(reloj, nowUtc, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en poll de reloj {RelojId}", reloj.IdReloj);
                    clockResult = new BackfillPollClockResultDto
                    {
                        RelojId = reloj.IdReloj,
                        DeviceSn = reloj.DeviceSn,
                        Status = "error",
                        Error = ex.Message,
                        CursorBefore = reloj.LastPollEvent,
                        CursorAfter = reloj.LastPollEvent
                    };
                }

                run.Clocks.Add(clockResult);
                run.TotalWindows += clockResult.WindowsProcessed;
                run.TotalPages += clockResult.PagesProcessed;
                run.Inserted += clockResult.Inserted;
                run.Duplicates += clockResult.Duplicates;
                run.Ignored += clockResult.Ignored;
            }

            run.Status = run.Clocks.Any(x => x.Status == "error") ? "partial_error" : "ok";
            return run;
        }
        catch (Exception ex)
        {
            run.Status = "error";
            run.Error = ex.Message;
            throw;
        }
        finally
        {
            run.FinishedAtUtc = DateTimeOffset.UtcNow;
            FinalizeRunSafely(run);
            RunLock.Release();
        }
    }

    public List<BackfillPollRunSummaryDto> ListarRuns(BackfillPollRunsQueryDto query)
    {
        return _pollRunsRepository
            .Search(query.Status, query.Limit, query.Offset)
            .Select(MapToSummary)
            .ToList();
    }

    public BackfillPollRunResultDto ObtenerRun(string runId)
    {
        var row = _pollRunsRepository.GetById(runId);
        if (row == null)
        {
            throw new ArgumentException("Run inexistente");
        }

        var clocks = DeserializeClocks(row.ClocksJson);

        return new BackfillPollRunResultDto
        {
            RunId = row.RunId,
            Trigger = row.Trigger,
            StartedAtUtc = row.StartedAtUtc,
            FinishedAtUtc = row.FinishedAtUtc ?? row.StartedAtUtc,
            Status = row.Status,
            Error = row.Error,
            TotalClocks = row.TotalClocks,
            TotalWindows = row.TotalWindows,
            TotalPages = row.TotalPages,
            Inserted = row.Inserted,
            Duplicates = row.Duplicates,
            Ignored = row.Ignored,
            Clocks = clocks
        };
    }

    private async Task<BackfillPollClockResultDto> ProcessClockAsync(Reloj reloj, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var result = new BackfillPollClockResultDto
        {
            RelojId = reloj.IdReloj,
            DeviceSn = reloj.DeviceSn,
            CursorBefore = reloj.LastPollEvent,
            CursorAfter = reloj.LastPollEvent
        };

        if (!IsClockReady(reloj, out var reason))
        {
            result.Status = "skipped";
            result.Note = reason;
            return result;
        }

        var windows = await ResolveWindowsAsync(reloj, nowUtc, result, ct);
        if (windows.Count == 0)
        {
            result.Status = "ok";
            result.CursorAfter = reloj.LastPollEvent;
            return result;
        }

        foreach (var window in windows)
        {
            ct.ThrowIfCancellationRequested();

            var polled = await PollWindowAsync(reloj, window.StartUtc, window.EndUtc, ct);

            result.WindowsProcessed++;
            result.PagesProcessed += polled.Pages;
            result.Inserted += polled.Inserted;
            result.Duplicates += polled.Duplicates;
            result.Ignored += polled.Ignored;

            // Cursor real de poll: se mueve al finalizar correctamente cada ventana.
            reloj.LastPollEvent = window.EndUtc;
            _relojesRepository.update(reloj);
            result.CursorAfter = reloj.LastPollEvent;
        }

        result.Status = "ok";
        return result;
    }

    private async Task<List<PollWindow>> ResolveWindowsAsync(
        Reloj reloj,
        DateTimeOffset nowUtc,
        BackfillPollClockResultDto result,
        CancellationToken ct)
    {
        var window = TimeSpan.FromMinutes(Math.Max(1, _options.WindowMinutes));

        if (!reloj.LastPollEvent.HasValue)
        {
            // Regla acordada: si no hay LastPollEvent, bootstrap oldest SIEMPRE.
            var oldest = await _hikvisionClient.GetOldestEventTimeAsync(
                reloj,
                _options.BootstrapStartUtc,
                nowUtc,
                maxResults: 1,
                ct);

            if (!oldest.HasValue)
            {
                // Si no hay historial en reloj, cursor arranca en now para pasar a seguridad 30m.
                reloj.LastPollEvent = nowUtc;
                _relojesRepository.update(reloj);
                result.Note = "bootstrap_sin_eventos";
                result.CursorAfter = reloj.LastPollEvent;
                return [];
            }

            if (oldest.Value >= nowUtc)
            {
                reloj.LastPollEvent = nowUtc;
                _relojesRepository.update(reloj);
                result.Note = "bootstrap_oldest_mayor_igual_now";
                result.CursorAfter = reloj.LastPollEvent;
                return [];
            }

            return SplitWindows(oldest.Value, nowUtc, window, _options.MaxWindowsPerRun);
        }

        var gap = nowUtc - reloj.LastPollEvent.Value;

        if (gap <= window)
        {
            // Modo seguridad cada 30 min cuando ya hay cursor.
            return [new PollWindow(nowUtc.Subtract(window), nowUtc)];
        }

        // Gap grande: catch-up por ventanas consecutivas de 30 min.
        return SplitWindows(reloj.LastPollEvent.Value, nowUtc, window, _options.MaxWindowsPerRun);
    }

    private async Task<WindowPollResult> PollWindowAsync(
        Reloj reloj,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken ct)
    {
        var pages = 0;
        var inserted = 0;
        var duplicates = 0;
        var ignored = 0;

        var searchId = Guid.NewGuid().ToString("N");
        var position = 0;

        while (true)
        {
            var response = await _hikvisionClient.SearchAsync(
                reloj: reloj,
                fromUtc: startUtc,
                toUtc: endUtc,
                searchId: searchId,
                searchResultPosition: position,
                maxResults: Math.Max(1, _options.MaxResultsPerPage),
                timeReverseOrder: false,
                ct: ct);

            pages++;

            var infoList = response.InfoList ?? [];
            if (infoList.Count > 0)
            {
                var ingest = _accesEventService.ProcesarEventosDesdePoll(reloj.IdReloj, reloj.DeviceSn!, infoList);
                inserted += ingest.Inserted;
                duplicates += ingest.Duplicates;
                ignored += ingest.Ignored;
            }

            if (response.NumOfMatches <= 0 || infoList.Count == 0)
            {
                break;
            }

            position += response.NumOfMatches;

            var hasMore = string.Equals(response.ResponseStatusStrg, "MORE", StringComparison.OrdinalIgnoreCase);
            if (!hasMore)
            {
                break;
            }
        }

        return new WindowPollResult(pages, inserted, duplicates, ignored);
    }

    private static bool IsClockReady(Reloj reloj, out string reason)
    {
        if (string.IsNullOrWhiteSpace(reloj.DeviceSn))
        {
            reason = "deviceSn_missing";
            return false;
        }

        if (reloj.Puerto <= 0)
        {
            reason = "puerto_invalido";
            return false;
        }

        if (reloj.Residential == null || string.IsNullOrWhiteSpace(reloj.Residential.IpActual))
        {
            reason = "residential_ip_missing";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static List<PollWindow> SplitWindows(
        DateTimeOffset startInclusiveUtc,
        DateTimeOffset endInclusiveUtc,
        TimeSpan windowSize,
        int maxWindows)
    {
        var windows = new List<PollWindow>();
        var cursor = startInclusiveUtc;

        while (cursor < endInclusiveUtc)
        {
            var next = cursor.Add(windowSize);
            if (next > endInclusiveUtc)
            {
                next = endInclusiveUtc;
            }

            windows.Add(new PollWindow(cursor, next));
            cursor = next;

            if (windows.Count > maxWindows)
            {
                throw new InvalidOperationException("Cantidad de ventanas excede el maximo permitido");
            }
        }

        return windows;
    }

    private void HydrateFromLastPersistedRun()
    {
        var last = _pollRunsRepository.GetLast();
        if (last == null)
        {
            return;
        }

        _status.IsRunning = false;
        _status.LastRunId = last.RunId;
        _status.LastTrigger = last.Trigger;
        _status.LastStartedAtUtc = last.StartedAtUtc;
        _status.LastFinishedAtUtc = last.FinishedAtUtc;
        _status.LastStatus = last.Status;
        _status.LastError = last.Error;
        _status.LastTotalClocks = last.TotalClocks;
        _status.LastInserted = last.Inserted;
        _status.LastDuplicates = last.Duplicates;
        _status.LastIgnored = last.Ignored;
    }

    private void PersistStartedRun(BackfillPollRunResultDto run)
    {
        var row = new BackfillPollRunLog
        {
            RunId = run.RunId,
            Trigger = run.Trigger,
            StartedAtUtc = run.StartedAtUtc,
            FinishedAtUtc = null,
            Status = "running",
            Error = null,
            TotalClocks = 0,
            TotalWindows = 0,
            TotalPages = 0,
            Inserted = 0,
            Duplicates = 0,
            Ignored = 0,
            ClocksJson = "[]"
        };

        _pollRunsRepository.AddStarted(row);
    }

    private void PersistFinishedRun(BackfillPollRunResultDto run)
    {
        var row = _pollRunsRepository.GetById(run.RunId);
        if (row == null)
        {
            row = new BackfillPollRunLog
            {
                RunId = run.RunId,
                Trigger = run.Trigger,
                StartedAtUtc = run.StartedAtUtc,
                FinishedAtUtc = run.FinishedAtUtc,
                Status = run.Status,
                Error = run.Error,
                TotalClocks = run.TotalClocks,
                TotalWindows = run.TotalWindows,
                TotalPages = run.TotalPages,
                Inserted = run.Inserted,
                Duplicates = run.Duplicates,
                Ignored = run.Ignored,
                ClocksJson = JsonSerializer.Serialize(run.Clocks)
            };

            _pollRunsRepository.AddStarted(row);
            return;
        }

        row.FinishedAtUtc = run.FinishedAtUtc;
        row.Status = run.Status;
        row.Error = run.Error;
        row.TotalClocks = run.TotalClocks;
        row.TotalWindows = run.TotalWindows;
        row.TotalPages = run.TotalPages;
        row.Inserted = run.Inserted;
        row.Duplicates = run.Duplicates;
        row.Ignored = run.Ignored;
        row.ClocksJson = JsonSerializer.Serialize(run.Clocks);

        _pollRunsRepository.UpdateFinished(row);
    }

    private void FinalizeRunSafely(BackfillPollRunResultDto run)
    {
        try
        {
            PersistFinishedRun(run);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo persistir cierre de corrida poll. RunId={RunId}", run.RunId);
        }

        try
        {
            SetFinishedStatus(run);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo actualizar estado en memoria de corrida poll. RunId={RunId}", run.RunId);
        }
    }

    private static BackfillPollStatusDto CloneStatus(BackfillPollStatusDto source)
    {
        return new BackfillPollStatusDto
        {
            IsRunning = source.IsRunning,
            LastRunId = source.LastRunId,
            LastTrigger = source.LastTrigger,
            LastStartedAtUtc = source.LastStartedAtUtc,
            LastFinishedAtUtc = source.LastFinishedAtUtc,
            LastStatus = source.LastStatus,
            LastError = source.LastError,
            LastTotalClocks = source.LastTotalClocks,
            LastInserted = source.LastInserted,
            LastDuplicates = source.LastDuplicates,
            LastIgnored = source.LastIgnored
        };
    }

    private static List<BackfillPollClockResultDto> DeserializeClocks(string? clocksJson)
    {
        if (string.IsNullOrWhiteSpace(clocksJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<BackfillPollClockResultDto>>(clocksJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static BackfillPollRunSummaryDto MapToSummary(BackfillPollRunLog row)
    {
        return new BackfillPollRunSummaryDto
        {
            RunId = row.RunId,
            Trigger = row.Trigger,
            StartedAtUtc = row.StartedAtUtc,
            FinishedAtUtc = row.FinishedAtUtc,
            Status = row.Status,
            Error = row.Error,
            TotalClocks = row.TotalClocks,
            TotalWindows = row.TotalWindows,
            TotalPages = row.TotalPages,
            Inserted = row.Inserted,
            Duplicates = row.Duplicates,
            Ignored = row.Ignored
        };
    }

    private static void SetRunningStatus(BackfillPollRunResultDto run)
    {
        lock (StatusSync)
        {
            _status.IsRunning = true;
            _status.LastRunId = run.RunId;
            _status.LastTrigger = run.Trigger;
            _status.LastStartedAtUtc = run.StartedAtUtc;
            _status.LastFinishedAtUtc = null;
            _status.LastStatus = "running";
            _status.LastError = null;
        }
    }

    private static void SetFinishedStatus(BackfillPollRunResultDto run)
    {
        lock (StatusSync)
        {
            _status.IsRunning = false;
            _status.LastRunId = run.RunId;
            _status.LastTrigger = run.Trigger;
            _status.LastStartedAtUtc = run.StartedAtUtc;
            _status.LastFinishedAtUtc = run.FinishedAtUtc;
            _status.LastStatus = run.Status;
            _status.LastError = run.Error;
            _status.LastTotalClocks = run.TotalClocks;
            _status.LastInserted = run.Inserted;
            _status.LastDuplicates = run.Duplicates;
            _status.LastIgnored = run.Ignored;
        }
    }

    private readonly record struct PollWindow(DateTimeOffset StartUtc, DateTimeOffset EndUtc);
    private readonly record struct WindowPollResult(int Pages, int Inserted, int Duplicates, int Ignored);
}
