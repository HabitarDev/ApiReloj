using Dominio;
using IDataAcces;
using IServices.IJornada;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dominio;
using Models.WebApi;
using NUlid;

namespace Service.JornadaServicess;

public class JornadaMantenimientoService(
    IJornadasRepository jornadasRepository,
    IResidentialsRepository residentialsRepository,
    IJornadaEntityService jornadaEntityService,
    IOptions<JornadaProcessingOptions> options,
    ILogger<JornadaMantenimientoService> logger) : IJornadaMantenimientoService
{
    private readonly IJornadasRepository _jornadasRepository = jornadasRepository;
    private readonly IResidentialsRepository _residentialsRepository = residentialsRepository;
    private readonly IJornadaEntityService _entity = jornadaEntityService;
    private readonly JornadaProcessingOptions _options = options.Value;
    private readonly ILogger<JornadaMantenimientoService> _logger = logger;

    public void ProcesarEventoInsertado(AccessEvents accessEvent)
    {
        if (string.IsNullOrWhiteSpace(accessEvent.EmployeeNumber) || string.IsNullOrWhiteSpace(accessEvent.DeviceSn))
        {
            _logger.LogWarning(
                "Evento sin employee/deviceSn; no se procesa jornada. DeviceSn={DeviceSn}, SerialNo={SerialNo}",
                accessEvent.DeviceSn, accessEvent.SerialNumber);
            return;
        }

        var eventType = Classify(accessEvent.AttendanceStatus);
        if (eventType == JornadaEventType.Unknown)
        {
            _logger.LogWarning(
                "attendanceStatus no mapeado; no impacta jornada. attendanceStatus={AttendanceStatus}, DeviceSn={DeviceSn}, SerialNo={SerialNo}",
                accessEvent.AttendanceStatus, accessEvent.DeviceSn, accessEvent.SerialNumber);
            return;
        }

        var open = _jornadasRepository.GetOpenByEmployeeAndClock(accessEvent.EmployeeNumber, accessEvent.DeviceSn);

        switch (eventType)
        {
            case JornadaEventType.CheckIn:
                HandleCheckIn(accessEvent, open);
                break;
            case JornadaEventType.BreakIn:
                HandleBreakIn(accessEvent, open);
                break;
            case JornadaEventType.BreakOut:
                HandleBreakOut(accessEvent, open);
                break;
            case JornadaEventType.CheckOut:
                HandleCheckOut(accessEvent, open);
                break;
        }
    }

    public int MarcarIncompletasVencidasComoError(DateTimeOffset nowUtc)
    {
        var cutoff = nowUtc.AddHours(-_options.IncompleteTimeoutHours);
        var open = _jornadasRepository.GetOpenOlderThan(cutoff);
        var updated = 0;

        foreach (var jornada in open)
        {
            jornada.StatusCheck = JornadaStatuses.Error;
            if (jornada.StatusBreak != JornadaStatuses.Ok)
            {
                jornada.StatusBreak = JornadaStatuses.Error;
            }

            jornada.UpdatedAt = nowUtc;
            _jornadasRepository.Update(jornada);
            updated++;
        }

        return updated;
    }

    public List<JornadaDto> Buscar(JornadasQueryDto query)
    {
        if (!query.ResidentialId.HasValue)
        {
            var rows = _jornadasRepository.Search(
                query.FromUtc,
                query.ToUtc,
                query.UpdatedSinceUtc,
                query.EmployeeNumber,
                query.ClockSn,
                query.StatusCheck,
                query.StatusBreak,
                query.Limit,
                query.Offset);

            return rows.Select(_entity.FromEntity).ToList();
        }

        var residential = _residentialsRepository.GetById(query.ResidentialId.Value);
        if (residential == null)
        {
            throw new ArgumentException("Residential inexistente");
        }

        var clockSnList = residential.Relojes
            .Select(r => r.DeviceSn)
            .Where(sn => !string.IsNullOrWhiteSpace(sn))
            .Select(sn => sn!)
            .Distinct()
            .ToList();

        if (!string.IsNullOrWhiteSpace(query.ClockSn))
        {
            if (!clockSnList.Contains(query.ClockSn))
            {
                return [];
            }

            clockSnList = [query.ClockSn];
        }

        if (clockSnList.Count == 0)
        {
            return [];
        }

        var merged = new List<Jornada>();
        foreach (var sn in clockSnList)
        {
            var rows = _jornadasRepository.Search(
                query.FromUtc,
                query.ToUtc,
                query.UpdatedSinceUtc,
                query.EmployeeNumber,
                sn,
                query.StatusCheck,
                query.StatusBreak,
                int.MaxValue,
                0);

            merged.AddRange(rows);
        }

        var paged = merged
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.StartAt)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToList();

        return paged.Select(_entity.FromEntity).ToList();
    }

    private void HandleCheckIn(AccessEvents accessEvent, Jornada? open)
    {
        if (open != null)
        {
            open.StatusCheck = JornadaStatuses.Error;
            if (open.StatusBreak != JornadaStatuses.Ok)
            {
                open.StatusBreak = JornadaStatuses.Error;
            }

            open.UpdatedAt = accessEvent.EventTimeUtc;
            _jornadasRepository.Update(open);
        }

        var created = new Jornada
        {
            JornadaId = Ulid.NewUlid().ToString(),
            EmployeeNumber = accessEvent.EmployeeNumber!,
            ClockSn = accessEvent.DeviceSn,
            StartAt = accessEvent.EventTimeUtc,
            StatusCheck = JornadaStatuses.Incomplete,
            StatusBreak = JornadaStatuses.Incomplete,
            UpdatedAt = accessEvent.EventTimeUtc
        };

        _jornadasRepository.Add(created);
    }

    private void HandleBreakIn(AccessEvents accessEvent, Jornada? open)
    {
        if (open == null)
        {
            CreateOrphanError(accessEvent, JornadaEventType.BreakIn);
            return;
        }

        if (open.BreakInAt == null && open.BreakOutAt == null)
        {
            open.BreakInAt = accessEvent.EventTimeUtc;
            open.StatusBreak = JornadaStatuses.Incomplete;
        }
        else
        {
            open.StatusBreak = JornadaStatuses.Error;
        }

        open.UpdatedAt = accessEvent.EventTimeUtc;
        _jornadasRepository.Update(open);
    }

    private void HandleBreakOut(AccessEvents accessEvent, Jornada? open)
    {
        if (open == null)
        {
            CreateOrphanError(accessEvent, JornadaEventType.BreakOut);
            return;
        }

        if (open.BreakInAt != null && open.BreakOutAt == null)
        {
            open.BreakOutAt = accessEvent.EventTimeUtc;
            open.StatusBreak = JornadaStatuses.Ok;
        }
        else
        {
            open.StatusBreak = JornadaStatuses.Error;
        }

        open.UpdatedAt = accessEvent.EventTimeUtc;
        _jornadasRepository.Update(open);
    }

    private void HandleCheckOut(AccessEvents accessEvent, Jornada? open)
    {
        if (open == null)
        {
            CreateOrphanError(accessEvent, JornadaEventType.CheckOut);
            return;
        }

        open.EndAt = accessEvent.EventTimeUtc;
        open.StatusCheck = open.StartAt.HasValue ? JornadaStatuses.Ok : JornadaStatuses.Error;

        if (open.StatusBreak != JornadaStatuses.Ok)
        {
            open.StatusBreak = JornadaStatuses.Error;
        }

        open.UpdatedAt = accessEvent.EventTimeUtc;
        _jornadasRepository.Update(open);
    }

    private void CreateOrphanError(AccessEvents accessEvent, JornadaEventType type)
    {
        var created = new Jornada
        {
            JornadaId = Ulid.NewUlid().ToString(),
            EmployeeNumber = accessEvent.EmployeeNumber!,
            ClockSn = accessEvent.DeviceSn,
            StartAt = null,
            StatusCheck = JornadaStatuses.Error,
            StatusBreak = type == JornadaEventType.BreakIn ? JornadaStatuses.Incomplete : JornadaStatuses.Error,
            UpdatedAt = accessEvent.EventTimeUtc
        };

        if (type == JornadaEventType.BreakIn)
        {
            created.BreakInAt = accessEvent.EventTimeUtc;
        }
        else if (type == JornadaEventType.BreakOut)
        {
            created.BreakOutAt = accessEvent.EventTimeUtc;
        }
        else if (type == JornadaEventType.CheckOut)
        {
            created.EndAt = accessEvent.EventTimeUtc;
        }

        _jornadasRepository.Add(created);
    }

    private JornadaEventType Classify(string? attendanceStatus)
    {
        if (string.IsNullOrWhiteSpace(attendanceStatus))
        {
            return JornadaEventType.Unknown;
        }

        var value = attendanceStatus.Trim();
        if (ContainsIgnoreCase(_options.AttendanceStatusMap.CheckIn, value)) return JornadaEventType.CheckIn;
        if (ContainsIgnoreCase(_options.AttendanceStatusMap.BreakIn, value)) return JornadaEventType.BreakIn;
        if (ContainsIgnoreCase(_options.AttendanceStatusMap.BreakOut, value)) return JornadaEventType.BreakOut;
        if (ContainsIgnoreCase(_options.AttendanceStatusMap.CheckOut, value)) return JornadaEventType.CheckOut;

        return JornadaEventType.Unknown;
    }

    private static bool ContainsIgnoreCase(IEnumerable<string>? options, string value)
    {
        if (options == null)
        {
            return false;
        }

        return options.Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
    }
}

internal enum JornadaEventType
{
    Unknown = 0,
    CheckIn = 1,
    BreakIn = 2,
    BreakOut = 3,
    CheckOut = 4
}
