using IServices.IBackfillPoll;
using Models.WebApi;

namespace Service.BackfillServicess;

public class BackfillPollValidationService : IBackfillPollValidationService
{
    public void Validar(BackfillPollRunRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ResidentialId.HasValue && request.ResidentialId.Value <= 0)
        {
            throw new ArgumentException("residentialId invalido");
        }

        if (request.RelojId.HasValue && request.RelojId.Value <= 0)
        {
            throw new ArgumentException("relojId invalido");
        }

        if (string.IsNullOrWhiteSpace(request.Trigger))
        {
            request.Trigger = "manual";
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
            string[] valid = ["running", "ok", "partial_error", "error"];
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
