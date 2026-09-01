using IServices.IBackfillPoll;
using Models.WebApi;

namespace Service.BackfillServicess;

public class BackfillPollValidationService : IBackfillPollValidationService
{
    public void Validar(BackfillPollRunRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ResidentialId != null && string.IsNullOrWhiteSpace(request.ResidentialId))
        {
            throw new ArgumentException("residentialId invalido");
        }

        if (request.RelojId != null && string.IsNullOrWhiteSpace(request.RelojId))
        {
            throw new ArgumentException("relojId invalido");
        }

        if (string.IsNullOrWhiteSpace(request.Trigger))
        {
            request.Trigger = BackfillPollTriggers.Manual;
        }

        if (!BackfillPollTriggers.IsValid(request.Trigger))
        {
            throw new ArgumentException("trigger invalido");
        }
    }

    public void ValidarHistorial(BackfillPollRunsQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit <= 0)
        {
            throw new ArgumentException("limit debe ser mayor a 0");
        }

        if (query.Offset < 0)
        {
            throw new ArgumentException("offset debe ser mayor o igual a 0");
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            string[] valid =
            [
                BackfillPollRunStatuses.Running,
                BackfillPollRunStatuses.Ok,
                BackfillPollRunStatuses.PartialError,
                BackfillPollRunStatuses.Error
            ];
            if (!valid.Contains(query.Status))
            {
                throw new ArgumentException("status invalido");
            }
        }
    }

    public void ValidarRunId(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("runId invalido");
        }
    }
}
