using System.Text.Json;
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
}
