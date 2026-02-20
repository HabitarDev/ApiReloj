using Dominio;
using IServices.IJornada;
using Models.WebApi;

namespace Service.JornadaServicess;

public class JornadaValidationService : IJornadaValidationService
{
    public void ValidarBusqueda(JornadasQueryDto query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var hasFrom = query.FromUtc.HasValue;
        var hasTo = query.ToUtc.HasValue;

        if (hasFrom != hasTo)
        {
            throw new ArgumentException("Para filtrar por fecha se requieren fromUtc y toUtc juntos");
        }

        if (hasFrom && query.FromUtc!.Value > query.ToUtc!.Value)
        {
            throw new ArgumentException("El rango de fechas es invalido");
        }

        if (query.Limit <= 0)
        {
            throw new ArgumentException("limit debe ser mayor a 0");
        }

        if (query.Offset < 0)
        {
            throw new ArgumentException("offset debe ser mayor o igual a 0");
        }

        ValidarStatus(query.StatusCheck, query.StatusBreak);
        query.StatusCheck = NormalizeStatus(query.StatusCheck);
        query.StatusBreak = NormalizeStatus(query.StatusBreak);
    }

    public void ValidarStatus(string? statusCheck, string? statusBreak)
    {
        string[] valid = [JornadaStatuses.Ok, JornadaStatuses.Incomplete, JornadaStatuses.Error];

        if (!string.IsNullOrWhiteSpace(statusCheck))
        {
            var normalized = NormalizeStatus(statusCheck);
            if (!valid.Contains(normalized))
            {
                throw new ArgumentException("statusCheck invalido");
            }
        }

        if (!string.IsNullOrWhiteSpace(statusBreak))
        {
            var normalized = NormalizeStatus(statusBreak);
            if (!valid.Contains(normalized))
            {
                throw new ArgumentException("statusBreak invalido");
            }
        }
    }

    private static string? NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status) ? status : status.Trim().ToUpperInvariant();
    }
}
