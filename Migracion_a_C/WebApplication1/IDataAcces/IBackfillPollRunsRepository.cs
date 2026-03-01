using Dominio;

namespace IDataAcces;

public interface IBackfillPollRunsRepository
{
    void AddStarted(BackfillPollRunLog run);
    void UpdateFinished(BackfillPollRunLog run);

    BackfillPollRunLog? GetById(string runId);
    BackfillPollRunLog? GetLast();

    List<BackfillPollRunLog> Search(string? status = null, int limit = 50, int offset = 0);
}
