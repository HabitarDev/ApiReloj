using System.Text.Json;
using Dominio;
using Microsoft.Extensions.Options;
using Models.WebApi;
using Service.JornadaServicess;
using Xunit;

namespace Service.Tests;

public class JornadaReconstructorTests
{
    private const string Employee = "EMP-1";
    private const string Residential = "RES-1";
    private readonly JornadaReconstructor _sut = new(Options.Create(new JornadaProcessingOptions
    {
        IncompleteTimeoutHours = 24,
        AttendanceStatusMap = new JornadaAttendanceMapOptions
        {
            CheckIn = ["checkIn"],
            BreakIn = ["breakIn"],
            BreakOut = ["breakOut"],
            CheckOut = ["checkOut"]
        }
    }));

    [Fact]
    public void Rebuild_AllowsNightShiftAcrossClocksInSameResidential()
    {
        var start = Utc(2026, 7, 10, 20, 0);
        var events = new[]
        {
            Event("CLOCK-A", 1, start, "checkIn"),
            Event("CLOCK-A", 2, start.AddHours(4), "breakIn"),
            Event("CLOCK-B", 1, start.AddHours(4.5), "breakOut"),
            Event("CLOCK-B", 2, start.AddHours(8), "checkOut")
        };

        var jornada = Assert.Single(_sut.Rebuild(Employee, Residential, events, start.AddHours(9)));

        Assert.Equal(JornadaStatuses.Ok, jornada.StatusCheck);
        Assert.Equal(JornadaStatuses.Ok, jornada.StatusBreak);
        Assert.Equal("CLOCK-A", jornada.StartDeviceSn);
        Assert.Equal("CLOCK-B", jornada.EndDeviceSn);
        Assert.Equal(Residential, jornada.ResidentialId);
    }

    [Fact]
    public void Rebuild_IsDeterministicWhenBackfillArrivesOutOfOrder()
    {
        var start = Utc(2026, 7, 1, 8, 0);
        var chronological = new[]
        {
            Event("CLOCK-A", 1, start, "checkIn"),
            Event("CLOCK-B", 1, start.AddHours(8), "checkOut"),
            Event("CLOCK-A", 2, start.AddDays(1), "checkIn"),
            Event("CLOCK-B", 2, start.AddDays(1).AddHours(8), "checkOut")
        };
        var backfillOrder = new[]
        {
            chronological[2], chronological[3], chronological[1], chronological[0]
        };

        var expected = _sut.Rebuild(Employee, Residential, chronological, start.AddDays(2));
        var actual = _sut.Rebuild(Employee, Residential, backfillOrder, start.AddDays(2));

        Assert.Equal(
            expected.Select(Signature).ToArray(),
            actual.Select(Signature).ToArray());
    }

    [Fact]
    public void Rebuild_KeepsFirstSemanticMarkAndAddsWarnings()
    {
        var start = Utc(2026, 7, 5, 8, 0);
        var events = new[]
        {
            Event("CLOCK-A", 1, start, "checkIn"),
            Event("CLOCK-A", 2, start.AddMinutes(5), "checkIn"),
            Event("CLOCK-A", 3, start.AddHours(4), "breakIn"),
            Event("CLOCK-A", 4, start.AddHours(4).AddMinutes(5), "breakIn"),
            Event("CLOCK-B", 1, start.AddHours(4.5), "breakOut"),
            Event("CLOCK-B", 2, start.AddHours(5), "breakIn"),
            Event("CLOCK-B", 3, start.AddHours(8), "checkOut"),
            Event("CLOCK-B", 4, start.AddHours(8).AddMinutes(5), "checkOut")
        };

        var jornada = Assert.Single(_sut.Rebuild(Employee, Residential, events, start.AddHours(9)));
        var warnings = Codes(jornada.WarningsJson);

        Assert.Equal(start, jornada.StartAt);
        Assert.Equal(start.AddHours(4), jornada.BreakInAt);
        Assert.Equal(start.AddHours(8), jornada.EndAt);
        Assert.Contains(JornadaIssueCodes.DuplicateCheckInIgnored, warnings);
        Assert.Contains(JornadaIssueCodes.DuplicateBreakInIgnored, warnings);
        Assert.Contains(JornadaIssueCodes.SecondBreakIgnored, warnings);
        Assert.Contains(JornadaIssueCodes.DuplicateCheckOutIgnored, warnings);
    }

    [Fact]
    public void Rebuild_ClosesAt24HoursAsErrorAndDoesNotAttachLaterEvent()
    {
        var start = Utc(2026, 7, 5, 8, 0);
        var events = new[]
        {
            Event("CLOCK-A", 1, start, "checkIn"),
            Event("CLOCK-B", 1, start.AddHours(25), "checkOut")
        };

        var jornadas = _sut.Rebuild(Employee, Residential, events, start.AddHours(26));

        Assert.Equal(2, jornadas.Count);
        Assert.Equal(JornadaStatuses.Error, jornadas[0].StatusCheck);
        Assert.Null(jornadas[0].EndAt);
        Assert.Contains(JornadaIssueCodes.MaximumDurationExceeded, Codes(jornadas[0].ErrorsJson));
        Assert.Null(jornadas[1].StartAt);
        Assert.Equal(start.AddHours(25), jornadas[1].EndAt);
        Assert.Contains(JornadaIssueCodes.MissingCheckIn, Codes(jornadas[1].ErrorsJson));
    }

    [Fact]
    public void Rebuild_ClosedShiftWithoutBreakUsesNoBreakStatus()
    {
        var start = Utc(2026, 7, 5, 8, 0);
        var events = new[]
        {
            Event("CLOCK-A", 1, start, "checkIn"),
            Event("CLOCK-B", 1, start.AddHours(8), "checkOut")
        };

        var jornada = Assert.Single(_sut.Rebuild(Employee, Residential, events, start.AddHours(9)));

        Assert.Equal(JornadaStatuses.Ok, jornada.StatusCheck);
        Assert.Equal(JornadaStatuses.NoBreak, jornada.StatusBreak);
        Assert.Empty(Codes(jornada.ErrorsJson));
    }

    [Fact]
    public void Rebuild_AcceptsCheckoutAtExactly24Hours()
    {
        var start = Utc(2026, 7, 5, 8, 0);
        var events = new[]
        {
            Event("CLOCK-A", 1, start, "checkIn"),
            Event("CLOCK-B", 1, start.AddHours(24), "checkOut")
        };

        var jornada = Assert.Single(_sut.Rebuild(Employee, Residential, events, start.AddHours(24)));

        Assert.Equal(JornadaStatuses.Ok, jornada.StatusCheck);
        Assert.Equal(start.AddHours(24), jornada.EndAt);
    }

    private static AccessEvents Event(
        string deviceSn,
        long serialNumber,
        DateTimeOffset at,
        string attendanceStatus)
    {
        return new AccessEvents(
            deviceSn,
            Residential,
            serialNumber,
            at,
            at.ToString("O"),
            Employee,
            5,
            1,
            attendanceStatus,
            "{}");
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
    }

    private static string Signature(Jornada jornada)
    {
        return string.Join('|',
            jornada.StartAt,
            jornada.BreakInAt,
            jornada.BreakOutAt,
            jornada.EndAt,
            jornada.StatusCheck,
            jornada.StatusBreak,
            jornada.WarningsJson,
            jornada.ErrorsJson);
    }

    private static List<string> Codes(string json)
    {
        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }
}
