using Dominio;

namespace IDataAcces;

public interface IJornadasRepository
{
    Jornada Add(Jornada jornada);
    Jornada? GetById(string jornadaId);
    List<Jornada> GetByProjectionKey(string employeeNumber, string residentialId);
    List<Jornada> Search(
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
        int offset = 0);
    List<(string EmployeeNumber, string ResidentialId, DateTimeOffset DirtyFromUtc)> GetIncompleteProjectionKeysOlderThan(
        DateTimeOffset cutoffUtc,
        int limit = 1000);
    void Update(Jornada jornada);
    void SaveProjection(IEnumerable<Jornada> newRows);
}
