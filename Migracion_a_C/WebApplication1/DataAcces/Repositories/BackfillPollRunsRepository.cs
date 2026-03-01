using DataAcces.Context;
using Dominio;
using IDataAcces;

namespace DataAcces.Repositories;

public class BackfillPollRunsRepository(SqlContext context) : IBackfillPollRunsRepository
{
    private readonly SqlContext _context = context;

    public void AddStarted(BackfillPollRunLog run)
    {
        _context.Set<BackfillPollRunLog>().Add(run);
        _context.SaveChanges();
    }

    public void UpdateFinished(BackfillPollRunLog run)
    {
        _context.Set<BackfillPollRunLog>().Update(run);
        _context.SaveChanges();
    }

    public BackfillPollRunLog? GetById(string runId)
    {
        return _context.Set<BackfillPollRunLog>()
            .FirstOrDefault(x => x.RunId == runId);
    }

    public BackfillPollRunLog? GetLast()
    {
        return _context.Set<BackfillPollRunLog>()
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefault();
    }

    public List<BackfillPollRunLog> Search(string? status = null, int limit = 50, int offset = 0)
    {
        var query = _context.Set<BackfillPollRunLog>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        return query
            .OrderByDescending(x => x.StartedAtUtc)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }
}
