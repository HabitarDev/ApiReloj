using Dominio;

namespace IDataAcces;

public interface IJornadaProjectionStateRepository
{
    void Enqueue(string employeeNumber, string residentialId, DateTimeOffset dirtyFromUtc);
    JornadaProjectionState? ClaimNext(DateTimeOffset nowUtc, int maxAttempts);
    void SaveChanges();
    void MarkFailure(
        string employeeNumber,
        string residentialId,
        string error,
        DateTimeOffset nextAttemptAtUtc,
        DateTimeOffset nowUtc);
    List<JornadaProjectionState> Search(string? status, int limit, int offset);
}
