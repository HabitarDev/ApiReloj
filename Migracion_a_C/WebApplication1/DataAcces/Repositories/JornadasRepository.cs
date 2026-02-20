using DataAcces.Context;
using Dominio;
using IDataAcces;

namespace DataAcces.Repositories;

public class JornadasRepository(SqlContext repos) : IJornadasRepository
{
    private readonly SqlContext _context = repos;

    public Jornada Add(Jornada jornada)
    {
        _context.Set<Jornada>().Add(jornada);
        _context.SaveChanges();
        return jornada;
    }

    public Jornada? GetById(string jornadaId)
    {
        return _context.Set<Jornada>().FirstOrDefault(x => x.JornadaId == jornadaId);
    }

    public Jornada? GetOpenByEmployeeAndClock(string employeeNumber, string clockSn)
    {
        return _context.Set<Jornada>()
            .Where(x => x.EmployeeNumber == employeeNumber
                        && x.ClockSn == clockSn
                        && x.StatusCheck == JornadaStatuses.Incomplete)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefault();
    }

    public List<Jornada> Search(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        DateTimeOffset? updatedSinceUtc = null,
        string? employeeNumber = null,
        string? clockSn = null,
        string? statusCheck = null,
        string? statusBreak = null,
        int limit = 100,
        int offset = 0)
    {
        var query = _context.Set<Jornada>().AsQueryable();

        if (fromUtc.HasValue && toUtc.HasValue)
        {
            query = query.Where(x => x.StartAt >= fromUtc.Value && x.StartAt <= toUtc.Value);
        }

        if (updatedSinceUtc.HasValue)
        {
            query = query.Where(x => x.UpdatedAt > updatedSinceUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(employeeNumber))
        {
            query = query.Where(x => x.EmployeeNumber == employeeNumber);
        }

        if (!string.IsNullOrWhiteSpace(clockSn))
        {
            query = query.Where(x => x.ClockSn == clockSn);
        }

        if (!string.IsNullOrWhiteSpace(statusCheck))
        {
            query = query.Where(x => x.StatusCheck == statusCheck);
        }

        if (!string.IsNullOrWhiteSpace(statusBreak))
        {
            query = query.Where(x => x.StatusBreak == statusBreak);
        }

        return query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.StartAt)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public List<Jornada> GetOpenOlderThan(DateTimeOffset cutoffUtc, int limit = 1000)
    {
        return _context.Set<Jornada>()
            .Where(x => x.StatusCheck == JornadaStatuses.Incomplete && x.UpdatedAt < cutoffUtc)
            .OrderBy(x => x.UpdatedAt)
            .Take(limit)
            .ToList();
    }

    public void Update(Jornada jornada)
    {
        _context.Set<Jornada>().Update(jornada);
        _context.SaveChanges();
    }
}
