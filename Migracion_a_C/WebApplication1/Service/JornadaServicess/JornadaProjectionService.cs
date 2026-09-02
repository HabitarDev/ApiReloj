using Dominio;
using IDataAcces;
using IServices.IJornada;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.WebApi;
using NUlid;

namespace Service.JornadaServicess;

public class JornadaProjectionService(
    IAccesEventsRepository accessEventsRepository,
    IJornadasRepository jornadasRepository,
    IJornadaProjectionStateRepository stateRepository,
    IDataTransactionManager transactionManager,
    JornadaReconstructor reconstructor,
    IOptions<JornadaProcessingOptions> options,
    ILogger<JornadaProjectionService> logger) : IJornadaProjectionService
{
    private readonly IAccesEventsRepository _accessEventsRepository = accessEventsRepository;
    private readonly IJornadasRepository _jornadasRepository = jornadasRepository;
    private readonly IJornadaProjectionStateRepository _stateRepository = stateRepository;
    private readonly IDataTransactionManager _transactionManager = transactionManager;
    private readonly JornadaReconstructor _reconstructor = reconstructor;
    private readonly JornadaProcessingOptions _options = options.Value;
    private readonly ILogger<JornadaProjectionService> _logger = logger;

    public bool ProcessNext(DateTimeOffset nowUtc)
    {
        JornadaProjectionState? state = null;
        var attemptNumber = 1;
        using var transaction = _transactionManager.BeginTransaction();

        try
        {
            state = _stateRepository.ClaimNext(nowUtc, Math.Max(1, _options.MaxAttempts));
            if (state == null)
            {
                transaction.Rollback();
                return false;
            }

            attemptNumber = state.Attempts + 1;
            state.Status = JornadaProjectionStateStatuses.Processing;
            state.Attempts = attemptNumber;
            state.StartedAtUtc = nowUtc;
            state.FinishedAtUtc = null;
            state.LastError = null;
            state.UpdatedAtUtc = nowUtc;

            var events = _accessEventsRepository.GetForProjection(
                state.EmployeeNumber,
                state.ResidentialId);
            var desired = _reconstructor.Rebuild(
                state.EmployeeNumber,
                state.ResidentialId,
                events,
                nowUtc);
            var existing = _jornadasRepository.GetByProjectionKey(
                state.EmployeeNumber,
                state.ResidentialId);

            var newRows = Reconcile(existing, desired, nowUtc);

            state.AppliedRevision = state.RequestedRevision;
            state.DirtyFromUtc = null;
            state.Status = JornadaProjectionStateStatuses.Ready;
            state.Attempts = 0;
            state.LastError = null;
            state.NextAttemptAtUtc = null;
            state.FinishedAtUtc = nowUtc;
            state.UpdatedAtUtc = nowUtc;

            _jornadasRepository.SaveProjection(newRows);
            transaction.Commit();
            return true;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            transaction.Dispose();
            _transactionManager.ClearTracking();

            if (state != null)
            {
                var exponent = Math.Min(10, Math.Max(0, attemptNumber - 1));
                var delaySeconds = Math.Max(1, _options.RetryBaseSeconds) * Math.Pow(2, exponent);
                var retryAt = nowUtc.AddSeconds(Math.Min(delaySeconds, 3600));
                _stateRepository.MarkFailure(
                    state.EmployeeNumber,
                    state.ResidentialId,
                    ex.ToString(),
                    retryAt,
                    nowUtc);

                _logger.LogError(
                    ex,
                    "Fallo proyeccion de jornada Employee={EmployeeNumber}, Residential={ResidentialId}, Attempt={Attempt}",
                    state.EmployeeNumber,
                    state.ResidentialId,
                    attemptNumber);
                return true;
            }

            throw;
        }
    }

    public int EnqueueExpired(DateTimeOffset nowUtc)
    {
        var cutoffUtc = nowUtc.AddHours(-Math.Max(1, _options.IncompleteTimeoutHours));
        var keys = _jornadasRepository.GetIncompleteProjectionKeysOlderThan(cutoffUtc);
        foreach (var key in keys)
        {
            _stateRepository.Enqueue(key.EmployeeNumber, key.ResidentialId, key.DirtyFromUtc);
        }

        return keys.Count;
    }

    public void RequestRebuild(JornadaRebuildRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EmployeeNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ResidentialId);

        _stateRepository.Enqueue(
            request.EmployeeNumber,
            request.ResidentialId,
            request.FromUtc ?? DateTimeOffset.MinValue);
    }

    public List<JornadaProjectionStateDto> GetStates(string? status, int limit, int offset)
    {
        return _stateRepository.Search(status, limit, offset)
            .Select(x => new JornadaProjectionStateDto
            {
                EmployeeNumber = x.EmployeeNumber,
                ResidentialId = x.ResidentialId,
                DirtyFromUtc = x.DirtyFromUtc,
                Status = x.Status,
                RequestedRevision = x.RequestedRevision,
                AppliedRevision = x.AppliedRevision,
                Attempts = x.Attempts,
                LastError = x.LastError,
                NextAttemptAtUtc = x.NextAttemptAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToList();
    }

    private static List<Jornada> Reconcile(
        IReadOnlyCollection<Jornada> existing,
        IReadOnlyCollection<Jornada> desired,
        DateTimeOffset nowUtc)
    {
        var existingByIdentity = existing
            .Where(x => x.IdentityDeviceSn != null && x.IdentitySerialNumber.HasValue)
            .ToDictionary(IdentityKey, StringComparer.Ordinal);
        var desiredKeys = new HashSet<string>(StringComparer.Ordinal);
        var newRows = new List<Jornada>();

        foreach (var projected in desired)
        {
            var key = IdentityKey(projected);
            desiredKeys.Add(key);

            if (existingByIdentity.TryGetValue(key, out var persisted))
            {
                if (ProjectionChanged(persisted, projected) || persisted.IsDeleted)
                {
                    CopyProjection(projected, persisted);
                    persisted.IsDeleted = false;
                    persisted.Revision = Math.Max(1, persisted.Revision + 1);
                    persisted.UpdatedAt = nowUtc;
                }
            }
            else
            {
                projected.JornadaId = Ulid.NewUlid().ToString();
                projected.Revision = 1;
                projected.IsDeleted = false;
                projected.CreatedAt = nowUtc;
                projected.UpdatedAt = nowUtc;
                newRows.Add(projected);
            }
        }

        foreach (var persisted in existing.Where(x => !x.IsDeleted))
        {
            if (persisted.IdentityDeviceSn == null
                || !persisted.IdentitySerialNumber.HasValue
                || !desiredKeys.Contains(IdentityKey(persisted)))
            {
                persisted.IsDeleted = true;
                persisted.ProjectionStatus = JornadaProjectionStatuses.Ready;
                persisted.Revision = Math.Max(1, persisted.Revision + 1);
                persisted.UpdatedAt = nowUtc;
            }
        }

        return newRows;
    }

    private static string IdentityKey(Jornada row)
    {
        return $"{row.IdentityDeviceSn}\u001f{row.IdentitySerialNumber}";
    }

    private static bool ProjectionChanged(Jornada left, Jornada right)
    {
        return left.EmployeeNumber != right.EmployeeNumber
               || left.ResidentialId != right.ResidentialId
               || left.ClockSn != right.ClockSn
               || left.StartAt != right.StartAt
               || left.BreakInAt != right.BreakInAt
               || left.BreakOutAt != right.BreakOutAt
               || left.EndAt != right.EndAt
               || left.StatusCheck != right.StatusCheck
               || left.StatusBreak != right.StatusBreak
               || left.StartDeviceSn != right.StartDeviceSn
               || left.StartSerialNumber != right.StartSerialNumber
               || left.BreakInDeviceSn != right.BreakInDeviceSn
               || left.BreakInSerialNumber != right.BreakInSerialNumber
               || left.BreakOutDeviceSn != right.BreakOutDeviceSn
               || left.BreakOutSerialNumber != right.BreakOutSerialNumber
               || left.EndDeviceSn != right.EndDeviceSn
               || left.EndSerialNumber != right.EndSerialNumber
               || left.WarningsJson != right.WarningsJson
               || left.ErrorsJson != right.ErrorsJson
               || left.ProjectionStatus != right.ProjectionStatus;
    }

    private static void CopyProjection(Jornada source, Jornada target)
    {
        target.EmployeeNumber = source.EmployeeNumber;
        target.ResidentialId = source.ResidentialId;
        target.ClockSn = source.ClockSn;
        target.StartAt = source.StartAt;
        target.BreakInAt = source.BreakInAt;
        target.BreakOutAt = source.BreakOutAt;
        target.EndAt = source.EndAt;
        target.StatusCheck = source.StatusCheck;
        target.StatusBreak = source.StatusBreak;
        target.IdentityDeviceSn = source.IdentityDeviceSn;
        target.IdentitySerialNumber = source.IdentitySerialNumber;
        target.StartDeviceSn = source.StartDeviceSn;
        target.StartSerialNumber = source.StartSerialNumber;
        target.BreakInDeviceSn = source.BreakInDeviceSn;
        target.BreakInSerialNumber = source.BreakInSerialNumber;
        target.BreakOutDeviceSn = source.BreakOutDeviceSn;
        target.BreakOutSerialNumber = source.BreakOutSerialNumber;
        target.EndDeviceSn = source.EndDeviceSn;
        target.EndSerialNumber = source.EndSerialNumber;
        target.WarningsJson = source.WarningsJson;
        target.ErrorsJson = source.ErrorsJson;
        target.ProjectionStatus = source.ProjectionStatus;
    }
}
