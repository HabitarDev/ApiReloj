using DataAcces.Context;
using Dominio;
using IDataAcces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DataAcces.Repositories;

public class AccessEventsRepository(SqlContext repos) : IAccesEventsRepository
{
    private readonly SqlContext _context = repos;

    public AccessEvents Add(AccessEvents accessEvent)
    {
        _context.AccessEvents.Add(accessEvent);
        _context.SaveChanges();
        return accessEvent;
    }

    public bool AddIfNotExists(AccessEvents accessEvent)
    {
        var exists = _context.AccessEvents.Any(x =>
            x.DeviceSn == accessEvent.DeviceSn &&
            x.SerialNumber == accessEvent.SerialNumber);
        if (exists)
        {
            return false;
        }

        try
        {
            _context.AccessEvents.Add(accessEvent);
            _context.SaveChanges();
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Si push y poll insertan al mismo tiempo, se toma como duplicado.
            return false;
        }
    }

    public int AddRangeIfNotExists(List<AccessEvents> accessEvents)
    {
        var inserted = 0;
        foreach (var accessEvent in accessEvents)
        {
            if (AddIfNotExists(accessEvent))
            {
                inserted++;
            }
        }

        return inserted;
    }

    public List<AccessEvents> GetBySerialNo(long id)
    {
        return _context.AccessEvents.Where(x => x.SerialNumber == id).ToList();
    }

    public List<AccessEvents> GetAll()
    {
        return _context.AccessEvents.ToList();
    }

    public List<AccessEvents> Search(
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        string? deviceSn = null,
        string? employeeNumber = null,
        int limit = 100,
        int offset = 0)
    {
        var query = _context.AccessEvents.AsQueryable();

        if (fromUtc.HasValue && toUtc.HasValue)
        {
            query = query.Where(x =>
                x.EventTimeUtc >= fromUtc.Value &&
                x.EventTimeUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(deviceSn))
        {
            query = query.Where(x => x.DeviceSn == deviceSn);
        }

        if (!string.IsNullOrWhiteSpace(employeeNumber))
        {
            query = query.Where(x => x.EmployeeNumber == employeeNumber);
        }

        return query
            .OrderByDescending(x => x.EventTimeUtc)
            .ThenByDescending(x => x.SerialNumber)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public List<AccessEvents> GetFromTime(string timeDevice)
    {
        if (!DateTimeOffset.TryParse(timeDevice, out var parsedTime))
        {
            throw new ArgumentException("Formato de fecha inválido", nameof(timeDevice));
        }

        return _context.AccessEvents
            .Where(x => x.EventTimeUtc >= parsedTime)
            .OrderByDescending(x => x.EventTimeUtc)
            .ThenByDescending(x => x.SerialNumber)
            .ToList();
    }

    public List<AccessEvents> GetByDeviceSnAndRange(
        string deviceSn,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string? employeeNo = null,
        string? attendanceStatus = null,
        int limit = 500,
        int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(deviceSn))
        {
            throw new ArgumentException("deviceSn invalido", nameof(deviceSn));
        }

        if (fromUtc > toUtc)
        {
            throw new ArgumentException("El rango de fechas es invalido");
        }

        if (limit <= 0)
        {
            limit = 500;
        }

        if (offset < 0)
        {
            offset = 0;
        }

        var query = _context.AccessEvents.Where(x =>
            x.DeviceSn == deviceSn &&
            x.EventTimeUtc >= fromUtc &&
            x.EventTimeUtc <= toUtc);

        if (!string.IsNullOrWhiteSpace(employeeNo))
        {
            query = query.Where(x => x.EmployeeNumber == employeeNo);
        }

        if (!string.IsNullOrWhiteSpace(attendanceStatus))
        {
            query = query.Where(x => x.AttendanceStatus == attendanceStatus);
        }

        return query
            .OrderByDescending(x => x.EventTimeUtc)
            .ThenByDescending(x => x.SerialNumber)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public void Update(AccessEvents accessEvent)
    {
        var exists = _context.AccessEvents.Any(x =>
            x.DeviceSn == accessEvent.DeviceSn &&
            x.SerialNumber == accessEvent.SerialNumber);
        if (!exists)
        {
            throw new InvalidOperationException("Evento inexistente");
        }

        _context.AccessEvents.Update(accessEvent);
        _context.SaveChanges();
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pg &&
               pg.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
