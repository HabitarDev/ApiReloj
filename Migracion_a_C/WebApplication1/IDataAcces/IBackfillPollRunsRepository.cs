using Dominio;

namespace IDataAcces;

public interface IBackfillPollRunsRepository
{
    void AddStarted(BackfillPollRunLog run);
    void Update(BackfillPollRunLog run);

    BackfillPollRunLog? GetById(string runId);
    BackfillPollRunLog? GetLast();
    List<BackfillPollRunLog> GetRunning();

    List<BackfillPollRunLog> Search(string? status = null, int limit = 50, int offset = 0);
}
