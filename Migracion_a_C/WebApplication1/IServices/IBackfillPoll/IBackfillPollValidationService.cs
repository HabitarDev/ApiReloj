using Models.WebApi;

namespace IServices.IBackfillPoll;

public interface IBackfillPollValidationService
{
    void Validar(BackfillPollRunRequestDto request);
    void ValidarHistorial(BackfillPollRunsQueryDto query);
    void ValidarRunId(string runId);
}
