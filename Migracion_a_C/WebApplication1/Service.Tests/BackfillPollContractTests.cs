using System.Text.Json;
using Dominio;
using IDataAcces;
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
        return new BackfillPollMantenimientoService(
            new StaticRelojesRepository(clocks),
            repository,
            null!,
            null!,
            Options.Create(new BackfillPollingOptions()),
            new FixedTimeProvider(Now),
            NullLogger<BackfillPollMantenimientoService>.Instance);
    }

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
        public void update(Reloj reloj) => throw new NotSupportedException();
        public void delete(string id) => throw new NotSupportedException();
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
