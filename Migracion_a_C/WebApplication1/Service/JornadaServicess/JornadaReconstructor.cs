using System.Text.Json;
using Dominio;
using Microsoft.Extensions.Options;
using Models.WebApi;

namespace Service.JornadaServicess;

/// <summary>
/// Proyeccion pura y determinista de eventos ISAPI a jornadas. Siempre ordena
/// por tiempo, reloj y serial, por lo que un backfill historico produce el mismo
/// resultado sin importar el orden en que los eventos llegaron a la API.
/// </summary>
public class JornadaReconstructor(IOptions<JornadaProcessingOptions> options)
{
    private readonly JornadaProcessingOptions _options = options.Value;

    public List<Jornada> Rebuild(
        string employeeNumber,
        string residentialId,
        IReadOnlyCollection<AccessEvents> sourceEvents,
        DateTimeOffset nowUtc)
    {
        var events = sourceEvents
            .Where(x => x.EmployeeNumber == employeeNumber && x.ResidentialId == residentialId)
            .OrderBy(x => x.EventTimeUtc)
            .ThenBy(x => x.SerialNumber)
            .ThenBy(x => x.DeviceSn, StringComparer.Ordinal)
            .ToList();

        var maximumDuration = TimeSpan.FromHours(Math.Max(1, _options.IncompleteTimeoutHours));
        var result = new List<Candidate>();
        Candidate? current = null;
        Candidate? lastClosed = null;

        foreach (var accessEvent in events)
        {
            var eventType = Classify(accessEvent.AttendanceStatus);
            if (eventType == JornadaEventType.Unknown)
            {
                continue;
            }

            if (current?.Row.StartAt is { } startAt && accessEvent.EventTimeUtc > startAt.Add(maximumDuration))
            {
                Expire(current);
                result.Add(current);
                current = null;
                lastClosed = null;
            }

            switch (eventType)
            {
                case JornadaEventType.CheckIn:
                    if (current == null)
                    {
                        current = CreateStarted(employeeNumber, residentialId, accessEvent);
                        lastClosed = null;
                    }
                    else
                    {
                        current.AddWarning(JornadaIssueCodes.DuplicateCheckInIgnored);
                    }
                    break;

                case JornadaEventType.BreakIn:
                    if (current == null)
                    {
                        result.Add(CreateOrphan(employeeNumber, residentialId, accessEvent, eventType));
                        lastClosed = null;
                    }
                    else if (current.Row.BreakInAt.HasValue && current.Row.BreakOutAt.HasValue)
                    {
                        current.AddWarning(JornadaIssueCodes.SecondBreakIgnored);
                    }
                    else if (current.Row.BreakInAt.HasValue || current.Row.BreakOutAt.HasValue)
                    {
                        current.AddWarning(JornadaIssueCodes.DuplicateBreakInIgnored);
                    }
                    else
                    {
                        SetBreakIn(current.Row, accessEvent);
                    }
                    break;

                case JornadaEventType.BreakOut:
                    if (current == null)
                    {
                        result.Add(CreateOrphan(employeeNumber, residentialId, accessEvent, eventType));
                        lastClosed = null;
                    }
                    else if (current.Row.BreakInAt.HasValue && current.Row.BreakOutAt.HasValue)
                    {
                        current.AddWarning(JornadaIssueCodes.SecondBreakIgnored);
                    }
                    else if (current.Row.BreakOutAt.HasValue)
                    {
                        current.AddWarning(JornadaIssueCodes.DuplicateBreakOutIgnored);
                    }
                    else
                    {
                        SetBreakOut(current.Row, accessEvent);
                        if (!current.Row.BreakInAt.HasValue)
                        {
                            current.AddError(JornadaIssueCodes.MissingBreakIn);
                        }
                    }
                    break;

                case JornadaEventType.CheckOut:
                    if (current != null)
                    {
                        SetEnd(current.Row, accessEvent);
                        Close(current);
                        result.Add(current);
                        lastClosed = current;
                        current = null;
                    }
                    else if (lastClosed?.Row.StartAt is { } closedStart
                             && accessEvent.EventTimeUtc <= closedStart.Add(maximumDuration))
                    {
                        lastClosed.AddWarning(JornadaIssueCodes.DuplicateCheckOutIgnored);
                    }
                    else
                    {
                        result.Add(CreateOrphan(employeeNumber, residentialId, accessEvent, eventType));
                        lastClosed = null;
                    }
                    break;
            }
        }

        if (current != null)
        {
            if (current.Row.StartAt is { } startAt && nowUtc > startAt.Add(maximumDuration))
            {
                Expire(current);
            }
            else
            {
                current.Row.StatusCheck = JornadaStatuses.Incomplete;
                current.Row.StatusBreak = ResolveOpenBreakStatus(current.Row);
            }

            result.Add(current);
        }

        return result
            .Select(x => x.Build())
            .OrderBy(x => ProjectionTime(x))
            .ThenBy(x => x.IdentityDeviceSn, StringComparer.Ordinal)
            .ThenBy(x => x.IdentitySerialNumber)
            .ToList();
    }

    private static Candidate CreateStarted(
        string employeeNumber,
        string residentialId,
        AccessEvents accessEvent)
    {
        var candidate = CreateBase(employeeNumber, residentialId, accessEvent);
        candidate.Row.StartAt = accessEvent.EventTimeUtc;
        candidate.Row.StartDeviceSn = accessEvent.DeviceSn;
        candidate.Row.StartSerialNumber = accessEvent.SerialNumber;
        return candidate;
    }

    private static Candidate CreateOrphan(
        string employeeNumber,
        string residentialId,
        AccessEvents accessEvent,
        JornadaEventType eventType)
    {
        var candidate = CreateBase(employeeNumber, residentialId, accessEvent);
        candidate.Row.StatusCheck = JornadaStatuses.Error;
        candidate.AddError(JornadaIssueCodes.MissingCheckIn);

        if (eventType == JornadaEventType.BreakIn)
        {
            SetBreakIn(candidate.Row, accessEvent);
            candidate.Row.StatusBreak = JornadaStatuses.Error;
            candidate.AddError(JornadaIssueCodes.MissingBreakOut);
        }
        else if (eventType == JornadaEventType.BreakOut)
        {
            SetBreakOut(candidate.Row, accessEvent);
            candidate.Row.StatusBreak = JornadaStatuses.Error;
            candidate.AddError(JornadaIssueCodes.MissingBreakIn);
        }
        else
        {
            SetEnd(candidate.Row, accessEvent);
            candidate.Row.StatusBreak = JornadaStatuses.NoBreak;
        }

        return candidate;
    }

    private static Candidate CreateBase(
        string employeeNumber,
        string residentialId,
        AccessEvents identityEvent)
    {
        return new Candidate(new Jornada
        {
            EmployeeNumber = employeeNumber,
            ResidentialId = residentialId,
            ClockSn = identityEvent.DeviceSn,
            IdentityDeviceSn = identityEvent.DeviceSn,
            IdentitySerialNumber = identityEvent.SerialNumber,
            StatusCheck = JornadaStatuses.Incomplete,
            StatusBreak = JornadaStatuses.Incomplete,
            ProjectionStatus = JornadaProjectionStatuses.Ready,
            WarningsJson = "[]",
            ErrorsJson = "[]"
        });
    }

    private static void SetBreakIn(Jornada row, AccessEvents accessEvent)
    {
        row.BreakInAt = accessEvent.EventTimeUtc;
        row.BreakInDeviceSn = accessEvent.DeviceSn;
        row.BreakInSerialNumber = accessEvent.SerialNumber;
    }

    private static void SetBreakOut(Jornada row, AccessEvents accessEvent)
    {
        row.BreakOutAt = accessEvent.EventTimeUtc;
        row.BreakOutDeviceSn = accessEvent.DeviceSn;
        row.BreakOutSerialNumber = accessEvent.SerialNumber;
    }

    private static void SetEnd(Jornada row, AccessEvents accessEvent)
    {
        row.EndAt = accessEvent.EventTimeUtc;
        row.EndDeviceSn = accessEvent.DeviceSn;
        row.EndSerialNumber = accessEvent.SerialNumber;
    }

    private static void Close(Candidate candidate)
    {
        candidate.Row.StatusCheck = JornadaStatuses.Ok;
        if (candidate.Row.BreakInAt.HasValue && candidate.Row.BreakOutAt.HasValue)
        {
            candidate.Row.StatusBreak = candidate.Errors.Contains(JornadaIssueCodes.MissingBreakIn)
                ? JornadaStatuses.Error
                : JornadaStatuses.Ok;
        }
        else if (!candidate.Row.BreakInAt.HasValue && !candidate.Row.BreakOutAt.HasValue)
        {
            candidate.Row.StatusBreak = JornadaStatuses.NoBreak;
        }
        else
        {
            candidate.Row.StatusBreak = JornadaStatuses.Error;
            candidate.AddError(candidate.Row.BreakInAt.HasValue
                ? JornadaIssueCodes.MissingBreakOut
                : JornadaIssueCodes.MissingBreakIn);
        }
    }

    private static void Expire(Candidate candidate)
    {
        candidate.Row.StatusCheck = JornadaStatuses.Error;
        candidate.AddError(JornadaIssueCodes.MissingCheckOut);
        candidate.AddError(JornadaIssueCodes.MaximumDurationExceeded);

        if (candidate.Row.BreakInAt.HasValue && candidate.Row.BreakOutAt.HasValue)
        {
            candidate.Row.StatusBreak = candidate.Errors.Contains(JornadaIssueCodes.MissingBreakIn)
                ? JornadaStatuses.Error
                : JornadaStatuses.Ok;
        }
        else if (!candidate.Row.BreakInAt.HasValue && !candidate.Row.BreakOutAt.HasValue)
        {
            candidate.Row.StatusBreak = JornadaStatuses.NoBreak;
        }
        else
        {
            candidate.Row.StatusBreak = JornadaStatuses.Error;
            candidate.AddError(candidate.Row.BreakInAt.HasValue
                ? JornadaIssueCodes.MissingBreakOut
                : JornadaIssueCodes.MissingBreakIn);
        }
    }

    private static string ResolveOpenBreakStatus(Jornada row)
    {
        return row.BreakInAt.HasValue && row.BreakOutAt.HasValue
            ? JornadaStatuses.Ok
            : JornadaStatuses.Incomplete;
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

    private static bool ContainsIgnoreCase(IEnumerable<string>? values, string value)
    {
        return values?.Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase)) == true;
    }

    private static DateTimeOffset ProjectionTime(Jornada row)
    {
        return row.StartAt ?? row.BreakInAt ?? row.BreakOutAt ?? row.EndAt ?? DateTimeOffset.MinValue;
    }

    private sealed class Candidate(Jornada row)
    {
        public Jornada Row { get; } = row;
        public HashSet<string> Warnings { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Errors { get; } = new(StringComparer.Ordinal);

        public void AddWarning(string code) => Warnings.Add(code);
        public void AddError(string code) => Errors.Add(code);

        public Jornada Build()
        {
            Row.WarningsJson = JsonSerializer.Serialize(Warnings.OrderBy(x => x));
            Row.ErrorsJson = JsonSerializer.Serialize(Errors.OrderBy(x => x));
            return Row;
        }
    }
}

public enum JornadaEventType
{
    Unknown = 0,
    CheckIn = 1,
    BreakIn = 2,
    BreakOut = 3,
    CheckOut = 4
}
