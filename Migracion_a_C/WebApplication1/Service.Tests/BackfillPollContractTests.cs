using System.Text.Json;
using Dominio;
using IDataAcces;
using IServices.IBackfillPoll;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.WebApi;
using Service.BackfillServicess;
using Xunit;

namespace Service.Tests;

public class BackfillPollContractTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Run_PersistsTheCompletePendingSnapshotBeforeProcessing()
    {
        var repository = new InMemoryPollRunsRepository();
        var service = Service(repository, new Reloj
        {
            IdReloj = "CLOCK-1",
            ResidentialId = "RES-1",
            DeviceSn = null,
            LastPollEvent = Now.AddMinutes(-30)
        });

        var result = await service.EjecutarAsync(
            new BackfillPollRunRequestDto { Trigger = BackfillPollTriggers.Manual },
            TestContext.Current.CancellationToken);

        var initial = repository.Snapshots.First();
        Assert.Equal(BackfillPollRunStatuses.Running, initial.Status);
        Assert.Null(initial.FinishedAtUtc);
        Assert.Equal(1, initial.TotalClocks);
        var pending = Assert.Single(Deserialize(initial.ClocksJson));
        Assert.Equal("CLOCK-1", pending.RelojId);
        Assert.Equal(BackfillPollClockStatuses.Pending, pending.Status);

        var final = repository.Snapshots.Last();
        Assert.Equal(BackfillPollRunStatuses.Ok, final.Status);
        Assert.NotNull(final.FinishedAtUtc);
        Assert.Equal(BackfillPollClockStatuses.Skipped, Assert.Single(Deserialize(final.ClocksJson)).Status);
        Assert.Equal(final.RunId, result.RunId);
    }

    [Fact]
    public async Task Run_PersistsProgressAfterEachClockAndCompletesSuccessfully()
    {
        var repository = new InMemoryPollRunsRepository();
        var firstCallObservedPendingSnapshot = false;
        var client = new StubHikvisionClient((_, _) =>
        {
            var initial = repository.Snapshots.First();
            firstCallObservedPendingSnapshot = Deserialize(initial.ClocksJson)
                .All(clock => clock.Status == BackfillPollClockStatuses.Pending);
            return Task.FromResult(EmptySearchResult());
        });
        var service = Service(repository, client, ReadyClock("CLOCK-1"), ReadyClock("CLOCK-2"));

        var result = await service.EjecutarAsync(
            new BackfillPollRunRequestDto { Trigger = BackfillPollTriggers.Manual },
            TestContext.Current.CancellationToken);

        Assert.True(firstCallObservedPendingSnapshot);
        var afterFirstClock = Deserialize(repository.Snapshots[1].ClocksJson);
        Assert.Equal(BackfillPollClockStatuses.Ok, afterFirstClock[0].Status);
        Assert.Equal(BackfillPollClockStatuses.Pending, afterFirstClock[1].Status);
        Assert.Equal(BackfillPollRunStatuses.Running, repository.Snapshots[1].Status);
        Assert.Equal(BackfillPollRunStatuses.Ok, result.Status);
        Assert.NotNull(result.FinishedAtUtc);
        Assert.All(result.Clocks, clock => Assert.NotEqual(BackfillPollClockStatuses.Pending, clock.Status));
    }

    [Fact]
    public async Task Run_UsesPartialErrorWhenOnlyOneClockFails()
    {
        var repository = new InMemoryPollRunsRepository();
        var client = new StubHikvisionClient((clock, _) =>
            clock.IdReloj == "CLOCK-2"
                ? Task.FromException<HikvisionAcsEventResultDto>(new HttpRequestException("clock unavailable"))
                : Task.FromResult(EmptySearchResult()));
        var service = Service(repository, client, ReadyClock("CLOCK-1"), ReadyClock("CLOCK-2"));

        var result = await service.EjecutarAsync(
            new BackfillPollRunRequestDto { Trigger = BackfillPollTriggers.Scheduled },
            TestContext.Current.CancellationToken);

        Assert.Equal(BackfillPollRunStatuses.PartialError, result.Status);
        Assert.Contains(result.Clocks, clock => clock.Status == BackfillPollClockStatuses.Ok);
        Assert.Contains(result.Clocks, clock => clock.Status == BackfillPollClockStatuses.Error);
        Assert.All(result.Clocks, clock => Assert.NotEqual(BackfillPollClockStatuses.Pending, clock.Status));
    }

    [Fact]
    public async Task Run_UsesErrorWhenEveryClockFails()
    {
        var repository = new InMemoryPollRunsRepository();
        var client = new StubHikvisionClient((_, _) =>
            Task.FromException<HikvisionAcsEventResultDto>(new HttpRequestException("clock unavailable")));
        var service = Service(repository, client, ReadyClock("CLOCK-1"));

        var result = await service.EjecutarAsync(
            new BackfillPollRunRequestDto { Trigger = BackfillPollTriggers.Manual },
            TestContext.Current.CancellationToken);

        Assert.Equal(BackfillPollRunStatuses.Error, result.Status);
        Assert.NotNull(result.FinishedAtUtc);
        Assert.Equal(BackfillPollClockStatuses.Error, Assert.Single(result.Clocks).Status);
    }

    [Fact]
    public async Task Cancellation_ClosesTheRunAndConvertsEveryPendingClockToError()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new InMemoryPollRunsRepository();
        var client = new StubHikvisionClient((_, ct) =>
        {
            cancellation.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(EmptySearchResult());
        });
        var service = Service(repository, client, ReadyClock("CLOCK-1"), ReadyClock("CLOCK-2"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.EjecutarAsync(
            new BackfillPollRunRequestDto { Trigger = BackfillPollTriggers.Manual },
            cancellation.Token));

        var final = repository.Snapshots.Last();
        Assert.Equal(BackfillPollRunStatuses.Error, final.Status);
        Assert.NotNull(final.FinishedAtUtc);
        Assert.All(
            Deserialize(final.ClocksJson),
            clock => Assert.Equal(BackfillPollClockStatuses.Error, clock.Status));
    }

    [Fact]
    public void Recovery_TerminatesOrphanedRunsAndPendingClocksIdempotently()
    {
        var repository = new InMemoryPollRunsRepository();
        repository.AddStarted(new BackfillPollRunLog
        {
            RunId = "RUN-1",
            Trigger = "startup",
            StartedAtUtc = Now.AddMinutes(-5),
            Status = BackfillPollRunStatuses.Running,
            TotalClocks = 1,
            ClocksJson = JsonSerializer.Serialize(new[]
            {
                new BackfillPollClockResultDto
                {
                    RelojId = "CLOCK-1",
                    Status = BackfillPollClockStatuses.Pending
                }
            })
        });
        var service = Service(repository);

        Assert.Equal(1, service.RecuperarRunsHuerfanos(Now));
        Assert.Equal(0, service.RecuperarRunsHuerfanos(Now.AddMinutes(1)));

        var recovered = repository.GetById("RUN-1")!;
        Assert.Equal(BackfillPollTriggers.Scheduled, recovered.Trigger);
        Assert.Equal(BackfillPollRunStatuses.Error, recovered.Status);
        Assert.Equal(Now, recovered.FinishedAtUtc);
        Assert.Equal(BackfillPollClockStatuses.Error, Assert.Single(Deserialize(recovered.ClocksJson)).Status);

        var listed = Assert.Single(service.ListarRuns(new BackfillPollRunsQueryDto
        {
            Status = BackfillPollRunStatuses.Error
        }));
        Assert.Equal("RUN-1", listed.RunId);
        Assert.Equal(BackfillPollRunStatuses.Error, service.ObtenerRun("RUN-1").Status);
        Assert.Throws<KeyNotFoundException>(() => service.ObtenerRun("missing"));
    }

    [Fact]
    public void RunningJson_UsesANullFinishedTimestamp()
    {
        var json = JsonSerializer.Serialize(
            new BackfillPollRunResultDto
            {
                RunId = "RUN-1",
                Trigger = BackfillPollTriggers.Manual,
                StartedAtUtc = Now,
                Status = BackfillPollRunStatuses.Running
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("finishedAtUtc").ValueKind);
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("scheduled")]
    public void Validation_AcceptsOnlyContractTriggers(string trigger)
    {
        new BackfillPollValidationService().Validar(new BackfillPollRunRequestDto { Trigger = trigger });
    }

    [Fact]
    public void Validation_RejectsTheLegacyStartupTrigger()
    {
        Assert.Throws<ArgumentException>(() =>
            new BackfillPollValidationService().Validar(
                new BackfillPollRunRequestDto { Trigger = "startup" }));
    }

    private static BackfillPollMantenimientoService Service(
        InMemoryPollRunsRepository repository,
        params Reloj[] clocks)
    {
        return Service(repository, new StubHikvisionClient(), clocks);
    }

    private static BackfillPollMantenimientoService Service(
        InMemoryPollRunsRepository repository,
        IHikvisionAcsEventClient hikvisionClient,
        params Reloj[] clocks)
    {
        return new BackfillPollMantenimientoService(
            new StaticRelojesRepository(clocks),
            repository,
            null!,
            hikvisionClient,
            Options.Create(new BackfillPollingOptions()),
            new FixedTimeProvider(Now),
            NullLogger<BackfillPollMantenimientoService>.Instance);
    }

    private static Reloj ReadyClock(string id) => new()
    {
        IdReloj = id,
        ResidentialId = "RES-1",
        Residential = new Residential { IdResidential = "RES-1", IpActual = "127.0.0.1" },
        DeviceSn = $"SN-{id}",
        Puerto = 80,
        LastPollEvent = Now.AddMinutes(-10)
    };

    private static HikvisionAcsEventResultDto EmptySearchResult() => new()
    {
        ResponseStatusStrg = "OK",
        NumOfMatches = 0,
        InfoList = []
    };

    private static List<BackfillPollClockResultDto> Deserialize(string json)
    {
        return JsonSerializer.Deserialize<List<BackfillPollClockResultDto>>(json) ?? [];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StaticRelojesRepository(IReadOnlyCollection<Reloj> clocks) : IRelojesRepository
    {
        public Reloj Add(Reloj reloj) => throw new NotSupportedException();
        public Reloj? GetById(string id) => clocks.SingleOrDefault(clock => clock.IdReloj == id);
        public List<Reloj> GetAll() => clocks.ToList();
        public List<Reloj> GetPollCandidates(string? residentialId = null, string? relojId = null) =>
            clocks.Where(clock => residentialId == null || clock.ResidentialId == residentialId)
                .Where(clock => relojId == null || clock.IdReloj == relojId)
                .ToList();
        public void update(Reloj reloj) { }
        public void delete(string id) => throw new NotSupportedException();
    }

    private sealed class StubHikvisionClient(
        Func<Reloj, CancellationToken, Task<HikvisionAcsEventResultDto>>? search = null)
        : IHikvisionAcsEventClient
    {
        public Task<HikvisionAcsEventResultDto> SearchAsync(
            Reloj reloj,
            DateTimeOffset fromUtc,
            DateTimeOffset toUtc,
            string searchId,
            int searchResultPosition,
            int maxResults,
            bool timeReverseOrder,
            CancellationToken ct) =>
            search?.Invoke(reloj, ct) ?? Task.FromResult(EmptySearchResult());

        public Task<DateTimeOffset?> GetOldestEventTimeAsync(
            Reloj reloj,
            DateTimeOffset bootstrapStartUtc,
            DateTimeOffset nowUtc,
            int maxResults,
            CancellationToken ct) => Task.FromResult<DateTimeOffset?>(null);
    }

    private sealed class InMemoryPollRunsRepository : IBackfillPollRunsRepository
    {
        private readonly Dictionary<string, BackfillPollRunLog> _rows = new();
        public List<BackfillPollRunLog> Snapshots { get; } = [];

        public void AddStarted(BackfillPollRunLog run)
        {
            _rows[run.RunId] = Clone(run);
            Snapshots.Add(Clone(run));
        }

        public void Update(BackfillPollRunLog run)
        {
            _rows[run.RunId] = Clone(run);
            Snapshots.Add(Clone(run));
        }

        public BackfillPollRunLog? GetById(string runId) =>
            _rows.TryGetValue(runId, out var row) ? Clone(row) : null;

        public BackfillPollRunLog? GetLast() => _rows.Values
            .OrderByDescending(row => row.StartedAtUtc)
            .Select(Clone)
            .FirstOrDefault();

        public List<BackfillPollRunLog> GetRunning() => _rows.Values
            .Where(row => row.Status == BackfillPollRunStatuses.Running)
            .Select(Clone)
            .ToList();

        public List<BackfillPollRunLog> Search(string? status = null, int limit = 50, int offset = 0) =>
            _rows.Values
                .Where(row => status == null || row.Status == status)
                .Skip(offset)
                .Take(limit)
                .Select(Clone)
                .ToList();

        private static BackfillPollRunLog Clone(BackfillPollRunLog source) => new()
        {
            RunId = source.RunId,
            Trigger = source.Trigger,
            StartedAtUtc = source.StartedAtUtc,
            FinishedAtUtc = source.FinishedAtUtc,
            Status = source.Status,
            Error = source.Error,
            TotalClocks = source.TotalClocks,
            TotalWindows = source.TotalWindows,
            TotalPages = source.TotalPages,
            Inserted = source.Inserted,
            Duplicates = source.Duplicates,
            Ignored = source.Ignored,
            ClocksJson = source.ClocksJson
        };
    }
}
