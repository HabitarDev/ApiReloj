using Dominio;
using Models.WebApi;

namespace IServices.IBackfillPoll;

public interface IHikvisionAcsEventClient
{
    Task<HikvisionAcsEventResultDto> SearchAsync(
        Reloj reloj,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string searchId,
        int searchResultPosition,
        int maxResults,
        bool timeReverseOrder,
        CancellationToken ct);

    Task<DateTimeOffset?> GetOldestEventTimeAsync(
        Reloj reloj,
        DateTimeOffset bootstrapStartUtc,
        DateTimeOffset nowUtc,
        int maxResults,
        CancellationToken ct);
}
