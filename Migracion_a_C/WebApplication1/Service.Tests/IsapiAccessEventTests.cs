using System.Text.Json;
using Dominio;
using IDataAcces;
using Models.Dominio;
using Models.WebApi;
using Service.AccesEventsServicess;
using Xunit;

namespace Service.Tests;

public class IsapiAccessEventTests
{
    [Fact]
    public void PushValidation_AcceptsOptionalOrMatchingDeviceId()
    {
        var sut = new AccesEventValidationService();
        var payload = PushPayload();

        payload.DeviceID = null;
        sut.ValidarEventoPush(payload, "CLOCK-A");

        payload.DeviceID = "clock-a";
        sut.ValidarEventoPush(payload, "CLOCK-A");
    }

    [Fact]
    public void PushValidation_RejectsADeviceIdFromAnotherClock()
    {
        var payload = PushPayload();
        payload.DeviceID = "CLOCK-B";

        var exception = Assert.Throws<UnauthorizedAccessException>(() =>
            new AccesEventValidationService().ValidarEventoPush(payload, "CLOCK-A"));

        Assert.Contains("DeviceSn", exception.Message);
    }

    [Fact]
    public void PushNormalization_KeepsIsapiDataAndAddsServerResidential()
    {
        var payload = PushPayload();
        var raw = JsonSerializer.Serialize(payload);

        var dto = new AccesEventEntityService().NormalizarDesdePush(
            payload,
            "CLOCK-A",
            "RES-1",
            "application/json",
            false,
            raw);

        Assert.Equal("CLOCK-A", dto._deviceSn);
        Assert.Equal("RES-1", dto._residentialId);
        Assert.Equal(42, dto._serialNumber);
        Assert.Equal("EMP-1", dto._employeeNumber);
        Assert.Equal("checkIn", dto._attendanceStatus);
        var envelope = JsonSerializer.Deserialize<AccessEventRawEnvelopeDto>(dto._raw);
        Assert.NotNull(envelope);
        Assert.Equal("push", envelope.Source);
        Assert.Equal(raw, envelope.Payload);
    }

    [Fact]
    public void Search_DelegatesTheAuthoritativeResidentialFilterToTheRepository()
    {
        var eventRow = new AccessEvents(
            "SN-OLD",
            "RES-1",
            7,
            new DateTimeOffset(2026, 7, 10, 20, 0, 0, TimeSpan.Zero),
            null,
            "EMP-1",
            5,
            1,
            "checkIn",
            "{}");
        var events = new RecordingAccessEventsRepository([eventRow]);
        var sut = new AccesEventMantentimientoService(
            events,
            null!,
            new ExistingResidentialsRepository("RES-1"),
            null!,
            null!,
            new AccesEventEntityService(),
            null!);

        var result = sut.Buscar(new AccessEventsQueryDto
        {
            ResidentialId = "RES-1",
            DeviceSn = "SN-OLD",
            Limit = 25,
            Offset = 2
        });

        Assert.Equal("RES-1", events.ResidentialId);
        Assert.Equal("SN-OLD", events.DeviceSn);
        Assert.Equal(25, events.Limit);
        Assert.Equal(2, events.Offset);
        Assert.Equal("RES-1", Assert.Single(result)._residentialId);
    }

    private static HikvisionEventNotificationAlertDto PushPayload()
    {
        return new HikvisionEventNotificationAlertDto
        {
            DateTime = "2026-07-10T20:00:00Z",
            EventType = "AccessControllerEvent",
            DeviceID = "CLOCK-A",
            AccessControllerEvent = new HikvisionAccessControllerEventDto
            {
                SerialNo = 42,
                EmployeeNoString = "EMP-1",
                MajorEventType = 5,
                SubEventType = 1,
                AttendanceStatus = "checkIn"
            }
        };
    }

    private sealed class ExistingResidentialsRepository(string id) : IResidentialsRepository
    {
        public Residential Add(Residential residential) => throw new NotSupportedException();
        public Residential? GetById(string requestedId) => requestedId == id
            ? new Residential { IdResidential = id }
            : null;
        public List<Residential> GetAll() => throw new NotSupportedException();
        public bool TryUpdateIp(string residentialId, string ipNueva) => throw new NotSupportedException();
        public void update(Residential residential) => throw new NotSupportedException();
        public void delete(string requestedId) => throw new NotSupportedException();
    }

    private sealed class RecordingAccessEventsRepository(List<AccessEvents> rows) : IAccesEventsRepository
    {
        public string? ResidentialId { get; private set; }
        public string? DeviceSn { get; private set; }
        public int Limit { get; private set; }
        public int Offset { get; private set; }

        public AccessEvents Add(AccessEvents accessEvent) => throw new NotSupportedException();
        public bool AddIfNotExists(AccessEvents accessEvent) => throw new NotSupportedException();
        public int AddRangeIfNotExists(List<AccessEvents> accessEvents) => throw new NotSupportedException();
        public List<AccessEvents> GetBySerialNo(long id) => throw new NotSupportedException();
        public List<AccessEvents> GetAll() => throw new NotSupportedException();
        public List<AccessEvents> GetForProjection(string employeeNumber, string residentialId) => throw new NotSupportedException();

        public List<AccessEvents> Search(
            DateTimeOffset? fromUtc = null,
            DateTimeOffset? toUtc = null,
            string? residentialId = null,
            string? deviceSn = null,
            string? employeeNumber = null,
            int? major = null,
            int? minor = null,
            string? attendanceStatus = null,
            int limit = 100,
            int offset = 0)
        {
            ResidentialId = residentialId;
            DeviceSn = deviceSn;
            Limit = limit;
            Offset = offset;
            return rows;
        }

        public void Update(AccessEvents accessEvent) => throw new NotSupportedException();
    }
}
