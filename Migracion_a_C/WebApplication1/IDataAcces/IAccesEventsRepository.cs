using Dominio;

namespace IDataAcces;

public interface IAccesEventsRepository
{
    AccessEvents Add(AccessEvents accessEvent);
    bool AddIfNotExists(AccessEvents accessEvent);
    int AddRangeIfNotExists(List<AccessEvents> accessEvents);

    List<AccessEvents> GetBySerialNo(long id);
    List<AccessEvents> GetAll();
    List<AccessEvents> Search(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? deviceSn = null,
        string? employeeNumber = null,
        int? major = null,
        int? minor = null,
        string? attendanceStatus = null,
        int limit = 100,
        int offset = 0);

    void Update(AccessEvents accessEvent);
}
