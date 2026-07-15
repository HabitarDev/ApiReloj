using DataAcces.Context;
using Dominio;
using IDataAcces;
using Microsoft.EntityFrameworkCore;

namespace DataAcces.Repositories;

public class JornadaProjectionStateRepository(SqlContext context) : IJornadaProjectionStateRepository
{
    private readonly SqlContext _context = context;

    public void Enqueue(string employeeNumber, string residentialId, DateTimeOffset dirtyFromUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(residentialId);

        var nowUtc = DateTimeOffset.UtcNow;
        _context.Database.ExecuteSqlInterpolated($"""
            INSERT INTO "JornadaProjectionStates"
                ("EmployeeNumber", "ResidentialId", "DirtyFromUtc", "Status",
                 "RequestedRevision", "AppliedRevision", "Attempts", "LastError",
                 "NextAttemptAtUtc", "StartedAtUtc", "FinishedAtUtc", "UpdatedAtUtc")
            VALUES
                ({employeeNumber}, {residentialId}, {dirtyFromUtc}, 'PENDING',
                 1, 0, 0, NULL, NULL, NULL, NULL, {nowUtc})
            ON CONFLICT ("EmployeeNumber", "ResidentialId") DO UPDATE SET
                "DirtyFromUtc" = CASE
                    WHEN "JornadaProjectionStates"."DirtyFromUtc" IS NULL THEN EXCLUDED."DirtyFromUtc"
                    ELSE LEAST("JornadaProjectionStates"."DirtyFromUtc", EXCLUDED."DirtyFromUtc")
                END,
                "Status" = 'PENDING',
                "RequestedRevision" = "JornadaProjectionStates"."RequestedRevision" + 1,
                "Attempts" = 0,
                "LastError" = NULL,
                "NextAttemptAtUtc" = NULL,
                "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
            """);
    }

    public JornadaProjectionState? ClaimNext(DateTimeOffset nowUtc, int maxAttempts)
    {
        return _context.JornadaProjectionStates
            .FromSqlInterpolated($"""
                SELECT *
                FROM "JornadaProjectionStates"
                WHERE "Status" IN ('PENDING', 'ERROR')
                  AND "Attempts" < {maxAttempts}
                  AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= {nowUtc})
                ORDER BY "DirtyFromUtc" NULLS FIRST, "UpdatedAtUtc"
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .AsTracking()
            .AsEnumerable()
            .FirstOrDefault();
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }

    public void MarkFailure(
        string employeeNumber,
        string residentialId,
        string error,
        DateTimeOffset nextAttemptAtUtc,
        DateTimeOffset nowUtc)
    {
        var truncated = error.Length <= 4000 ? error : error[..4000];
        _context.Database.ExecuteSqlInterpolated($"""
            UPDATE "JornadaProjectionStates"
            SET "Status" = 'ERROR',
                "Attempts" = "Attempts" + 1,
                "LastError" = {truncated},
                "NextAttemptAtUtc" = {nextAttemptAtUtc},
                "FinishedAtUtc" = {nowUtc},
                "UpdatedAtUtc" = {nowUtc}
            WHERE "EmployeeNumber" = {employeeNumber}
              AND "ResidentialId" = {residentialId}
            """);
    }

    public List<JornadaProjectionState> Search(string? status, int limit, int offset)
    {
        var query = _context.JornadaProjectionStates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        return query
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, 1000))
            .ToList();
    }
}
