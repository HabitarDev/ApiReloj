using DataAcces.Context;
using DataAcces.Repositories;
using DataAcces.Transactions;
using Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.WebApi;
using Npgsql;
using Service.JornadaServicess;
using Xunit;

namespace Service.Tests;

public class PostgresProjectionIntegrationTests
{
    private static readonly JornadaProcessingOptions ProcessingOptions = new()
    {
        IncompleteTimeoutHours = 24,
        MaxAttempts = 3,
        RetryBaseSeconds = 1,
        AttendanceStatusMap = new JornadaAttendanceMapOptions
        {
            CheckIn = ["checkIn"],
            BreakIn = ["breakIn"],
            BreakOut = ["breakOut"],
            CheckOut = ["checkOut"]
        }
    };

    [Fact]
    public async Task HeartbeatAcceptance_IsAtomicAndOnlyOneConcurrentRequestWins()
    {
        var connectionString = TestConnectionString();
        const string residentialId = "SECURITY-RES-1";
        const string deviceId = "SECURITY-DEVICE-1";
        var originalTimestamp = new DateTimeOffset(2026, 7, 15, 1, 0, 0, TimeSpan.Zero);

        using (var setup = CreateContext(connectionString))
        {
            setup.Devices.RemoveRange(setup.Devices.Where(x => x.DeviceId == deviceId));
            setup.Residentials.RemoveRange(setup.Residentials.Where(x => x.IdResidential == residentialId));
            setup.SaveChanges();
            setup.Residentials.Add(new Residential
            {
                IdResidential = residentialId,
                IpActual = "198.51.100.1"
            });
            setup.Devices.Add(new Device
            {
                DeviceId = deviceId,
                ResidentialId = residentialId,
                SecretKey = "security-test-secret"
            });
            setup.SaveChanges();
        }

        using (var acceptedContext = CreateContext(connectionString))
        {
            var transactions = new EfDataTransactionManager(acceptedContext);
            using var transaction = transactions.BeginTransaction();
            var accepted = new DevicesRepository(acceptedContext).TryAcceptHeartbeat(
                deviceId,
                residentialId,
                originalTimestamp.ToUnixTimeSeconds(),
                originalTimestamp.UtcDateTime);
            Assert.True(accepted);
            Assert.True(new ResidentialsRepository(acceptedContext).TryUpdateIp(
                residentialId,
                "198.51.100.2"));
            transaction.Commit();
        }

        using (var rollbackContext = CreateContext(connectionString))
        {
            var transactions = new EfDataTransactionManager(rollbackContext);
            using var transaction = transactions.BeginTransaction();
            Assert.True(new DevicesRepository(rollbackContext).TryAcceptHeartbeat(
                deviceId,
                residentialId,
                originalTimestamp.AddSeconds(1).ToUnixTimeSeconds(),
                originalTimestamp.AddSeconds(1).UtcDateTime));
            Assert.True(new ResidentialsRepository(rollbackContext).TryUpdateIp(
                residentialId,
                "198.51.100.99"));
            transaction.Rollback();
        }

        using (var verification = CreateContext(connectionString))
        {
            var device = verification.Devices.AsNoTracking().Single(x => x.DeviceId == deviceId);
            var residential = verification.Residentials.AsNoTracking().Single(x => x.IdResidential == residentialId);
            Assert.Equal(originalTimestamp.ToUnixTimeSeconds(), device.LastAcceptedHeartbeatTimestamp);
            Assert.Equal("198.51.100.2", residential.IpActual);
        }

        var concurrentTimestamp = originalTimestamp.AddSeconds(2);
        using var start = new ManualResetEventSlim(false);
        async Task<bool> Attempt(string ip)
        {
            return await Task.Run(() =>
            {
                using var context = CreateContext(connectionString);
                using var transaction = new EfDataTransactionManager(context).BeginTransaction();
                start.Wait();
                var accepted = new DevicesRepository(context).TryAcceptHeartbeat(
                    deviceId,
                    residentialId,
                    concurrentTimestamp.ToUnixTimeSeconds(),
                    concurrentTimestamp.UtcDateTime);
                if (accepted)
                {
                    Assert.True(new ResidentialsRepository(context).TryUpdateIp(residentialId, ip));
                    transaction.Commit();
                }
                else
                {
                    transaction.Rollback();
                }

                return accepted;
            });
        }

        var attempts = new[] { Attempt("198.51.100.3"), Attempt("198.51.100.4") };
        start.Set();
        var results = await Task.WhenAll(attempts);
        Assert.Single(results, x => x);

        using (var finalVerification = CreateContext(connectionString))
        {
            var device = finalVerification.Devices.AsNoTracking().Single(x => x.DeviceId == deviceId);
            Assert.Equal(concurrentTimestamp.ToUnixTimeSeconds(), device.LastAcceptedHeartbeatTimestamp);
        }
    }

    [Fact]
    public void QueueClaim_SkipsRowsLockedByAnotherWorker()
    {
        var connectionString = TestConnectionString();
        using (var setupContext = CreateContext(connectionString))
        {
            ResetProjectionTables(setupContext);
            var setupRepository = new JornadaProjectionStateRepository(setupContext);
            setupRepository.Enqueue("EMP-1", "RES-1", DateTimeOffset.UtcNow.AddMinutes(-2));
            setupRepository.Enqueue("EMP-2", "RES-1", DateTimeOffset.UtcNow.AddMinutes(-1));
        }

        using var firstContext = CreateContext(connectionString);
        using var secondContext = CreateContext(connectionString);
        using var firstTransaction = new EfDataTransactionManager(firstContext).BeginTransaction();
        using var secondTransaction = new EfDataTransactionManager(secondContext).BeginTransaction();

        var first = new JornadaProjectionStateRepository(firstContext)
            .ClaimNext(DateTimeOffset.UtcNow, 3);
        var second = new JornadaProjectionStateRepository(secondContext)
            .ClaimNext(DateTimeOffset.UtcNow, 3);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.EmployeeNumber, second.EmployeeNumber);
        firstTransaction.Rollback();
        secondTransaction.Rollback();
    }

    [Fact]
    public void EventAndQueue_AreAtomicAndProjectACompleteCrossClockShift()
    {
        var connectionString = TestConnectionString();
        var start = new DateTimeOffset(2026, 7, 10, 20, 0, 0, TimeSpan.Zero);

        using (var rollbackContext = CreateContext(connectionString))
        {
            ResetProjectionTables(rollbackContext);
            var transactionManager = new EfDataTransactionManager(rollbackContext);
            using var transaction = transactionManager.BeginTransaction();
            var eventsRepository = new AccessEventsRepository(rollbackContext);
            var stateRepository = new JornadaProjectionStateRepository(rollbackContext);
            var accessEvent = Event("CLOCK-A", 1, start, "checkIn");

            Assert.True(eventsRepository.AddIfNotExists(accessEvent));
            stateRepository.Enqueue("EMP-1", "RES-1", start);
            transaction.Rollback();
        }

        using (var verificationContext = CreateContext(connectionString))
        {
            Assert.Empty(verificationContext.AccessEvents);
            Assert.Empty(verificationContext.JornadaProjectionStates);
        }

        using (var ingestionContext = CreateContext(connectionString))
        {
            var transactionManager = new EfDataTransactionManager(ingestionContext);
            using var transaction = transactionManager.BeginTransaction();
            var eventsRepository = new AccessEventsRepository(ingestionContext);
            var stateRepository = new JornadaProjectionStateRepository(ingestionContext);

            Assert.True(eventsRepository.AddIfNotExists(Event("CLOCK-A", 1, start, "checkIn")));
            stateRepository.Enqueue("EMP-1", "RES-1", start);
            Assert.True(eventsRepository.AddIfNotExists(Event("CLOCK-B", 1, start.AddHours(8), "checkOut")));
            stateRepository.Enqueue("EMP-1", "RES-1", start.AddHours(8));
            transaction.Commit();
        }

        using (var projectionContext = CreateContext(connectionString))
        {
            var options = Options.Create(ProcessingOptions);
            var service = new JornadaProjectionService(
                new AccessEventsRepository(projectionContext),
                new JornadasRepository(projectionContext),
                new JornadaProjectionStateRepository(projectionContext),
                new EfDataTransactionManager(projectionContext),
                new JornadaReconstructor(options),
                options,
                NullLogger<JornadaProjectionService>.Instance);

            Assert.True(service.ProcessNext(start.AddHours(9)));
        }

        using (var verificationContext = CreateContext(connectionString))
        {
            var jornada = Assert.Single(verificationContext.Jornadas.AsNoTracking());
            var state = Assert.Single(verificationContext.JornadaProjectionStates.AsNoTracking());

            Assert.Equal(JornadaStatuses.Ok, jornada.StatusCheck);
            Assert.Equal(JornadaStatuses.NoBreak, jornada.StatusBreak);
            Assert.Equal("CLOCK-A", jornada.StartDeviceSn);
            Assert.Equal("CLOCK-B", jornada.EndDeviceSn);
            Assert.Equal(JornadaProjectionStateStatuses.Ready, state.Status);
            Assert.Equal(state.RequestedRevision, state.AppliedRevision);
        }
    }

    [Fact]
    public void AccessEventSearch_FiltersByStoredResidentialAndUsesDeterministicOrder()
    {
        var connectionString = TestConnectionString();
        var at = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

        using (var setup = CreateContext(connectionString))
        {
            ResetProjectionTables(setup);
            setup.AccessEvents.AddRange(
                Event("SN-A", "RES-A", 11, at, "checkIn"),
                Event("SN-B", "RES-A", 11, at, "checkIn"),
                Event("SN-C", "RES-B", 11, at, "checkIn"));
            setup.SaveChanges();
        }

        using var context = CreateContext(connectionString);
        var rows = new AccessEventsRepository(context).Search(
            residentialId: "RES-A",
            attendanceStatus: "CHECKIN",
            limit: 10,
            offset: 0);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("RES-A", row.ResidentialId));
        Assert.Equal(["SN-B", "SN-A"], rows.Select(row => row.DeviceSn));

        var combined = new AccessEventsRepository(context).Search(
            fromUtc: at,
            toUtc: at,
            residentialId: "RES-A",
            deviceSn: "SN-B",
            employeeNumber: "EMP-1",
            major: 5,
            minor: 1,
            attendanceStatus: "CHECKIN",
            limit: 1,
            offset: 0);
        Assert.Equal("SN-B", Assert.Single(combined).DeviceSn);

        var secondPage = new AccessEventsRepository(context).Search(
            residentialId: "RES-A",
            attendanceStatus: "checkIn",
            limit: 1,
            offset: 1);
        Assert.Equal("SN-A", Assert.Single(secondPage).DeviceSn);
    }

    private static string TestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("APIRELOJ_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("APIRELOJ_TEST_CONNECTION no esta configurada");
        }

        if (!string.Equals(
                Environment.GetEnvironmentVariable("APIRELOJ_ALLOW_DESTRUCTIVE_TESTS"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Las integraciones eliminan datos de sus tablas. " +
                "Configure APIRELOJ_ALLOW_DESTRUCTIVE_TESTS=true solamente para una base aislada.");
        }

        var parsedConnection = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(parsedConnection.Database) ||
            !parsedConnection.Database.EndsWith("_tests", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "APIRELOJ_TEST_CONNECTION debe apuntar a una base cuyo nombre termine en _tests.");
        }

        return connectionString;
    }

    private static SqlContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SqlContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new SqlContext(options);
    }

    private static void ResetProjectionTables(SqlContext context)
    {
        context.Database.ExecuteSqlRaw("""
            DELETE FROM "Jornadas";
            DELETE FROM "AccessEvents";
            DELETE FROM "JornadaProjectionStates";
            """);
    }

    private static AccessEvents Event(
        string deviceSn,
        long serialNumber,
        DateTimeOffset at,
        string attendanceStatus)
    {
        return Event(deviceSn, "RES-1", serialNumber, at, attendanceStatus);
    }

    private static AccessEvents Event(
        string deviceSn,
        string residentialId,
        long serialNumber,
        DateTimeOffset at,
        string attendanceStatus)
    {
        return new AccessEvents(
            deviceSn,
            residentialId,
            serialNumber,
            at,
            at.ToString("O"),
            "EMP-1",
            5,
            1,
            attendanceStatus,
            "{}");
    }
}
