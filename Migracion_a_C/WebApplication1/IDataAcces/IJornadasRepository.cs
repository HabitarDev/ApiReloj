using Dominio;

namespace IDataAcces;

public interface IJornadasRepository
{
    Jornada Add(Jornada jornada);
    Jornada? GetById(string jornadaId);
    Jornada? GetOpenByEmployeeAndClock(string employeeNumber, string clockSn);
    List<Jornada> Search(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        DateTimeOffset? updatedSinceUtc = null,
        string? employeeNumber = null,
        string? clockSn = null,
        string? statusCheck = null,
        string? statusBreak = null,
        int limit = 100,
        int offset = 0);
    List<Jornada> GetOpenOlderThan(DateTimeOffset cutoffUtc, int limit = 1000);
    void Update(Jornada jornada);
}
