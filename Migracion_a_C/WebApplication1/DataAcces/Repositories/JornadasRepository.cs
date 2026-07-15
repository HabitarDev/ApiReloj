using DataAcces.Context;
using Dominio;
using IDataAcces;
using Microsoft.EntityFrameworkCore;

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

    public List<Jornada> GetByProjectionKey(string employeeNumber, string residentialId)
    {
        return _context.Set<Jornada>()
            .Where(x => x.EmployeeNumber == employeeNumber && x.ResidentialId == residentialId)
            .OrderBy(x => x.CreatedAt)
            .ToList();
    }

    public List<Jornada> Search(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        DateTimeOffset? updatedSinceUtc = null,
        string? employeeNumber = null,
        string? residentialId = null,
        string? clockSn = null,
        string? statusCheck = null,
        string? statusBreak = null,
        string? projectionStatus = null,
        bool includeDeleted = false,
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

        if (!string.IsNullOrWhiteSpace(residentialId))
        {
            query = query.Where(x => x.ResidentialId == residentialId);
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

        if (!string.IsNullOrWhiteSpace(projectionStatus))
        {
            query = query.Where(x => x.ProjectionStatus == projectionStatus);
        }

        if (!includeDeleted)
        {
            query = query.Where(x => !x.IsDeleted);
        }

        return query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.StartAt)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public List<(string EmployeeNumber, string ResidentialId, DateTimeOffset DirtyFromUtc)> GetIncompleteProjectionKeysOlderThan(
        DateTimeOffset cutoffUtc,
        int limit = 1000)
    {
        var rows = _context.Set<Jornada>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.StatusCheck == JornadaStatuses.Incomplete
                        && x.StartAt.HasValue
                        && x.StartAt.Value < cutoffUtc)
            .Select(x => new { x.EmployeeNumber, x.ResidentialId, DirtyFromUtc = x.StartAt!.Value })
            .Distinct()
            .OrderBy(x => x.DirtyFromUtc)
            .Take(limit)
            .ToList();

        return rows
            .Select(x => (x.EmployeeNumber, x.ResidentialId, x.DirtyFromUtc))
            .ToList();
    }

    public void Update(Jornada jornada)
    {
        _context.Set<Jornada>().Update(jornada);
        _context.SaveChanges();
    }

    public void SaveProjection(IEnumerable<Jornada> newRows)
    {
        var rows = newRows.ToList();
        if (rows.Count > 0)
        {
            _context.Set<Jornada>().AddRange(rows);
        }

        _context.SaveChanges();
    }
}
