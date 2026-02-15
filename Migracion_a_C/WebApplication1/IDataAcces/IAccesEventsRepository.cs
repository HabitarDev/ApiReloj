using Dominio;

namespace IDataAcces;

public interface IAccesEventsRepository
{
    AccessEvents Add(AccessEvents accessEvent);
    bool AddIfNotExists(AccessEvents accessEvent);
    int AddRangeIfNotExists(List<AccessEvents> accessEvents);

    List<AccessEvents> GetBySerialNo(long id);
    List<AccessEvents> GetAll();
    List<AccessEvents> GetFromTime(string timeDevice);
    List<AccessEvents> GetByDeviceSnAndRange(
        string deviceSn,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? employeeNo = null,
        string? attendanceStatus = null,
        int limit = 500,
        int offset = 0);

    void Update(AccessEvents accessEvent);
}
