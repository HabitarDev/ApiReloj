using System.Net;
using System.Text;
using System.Text.Json;
using Dominio;
using IServices.IBackfillPoll;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.WebApi;

namespace Service.BackfillServicess;

public class HikvisionAcsEventClient(
    IOptions<BackfillPollingOptions> options,
    ILogger<HikvisionAcsEventClient> logger) : IHikvisionAcsEventClient
{
    private readonly BackfillPollingOptions _options = options.Value;
    private readonly ILogger<HikvisionAcsEventClient> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<HikvisionAcsEventResultDto> SearchAsync(
        Reloj reloj,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string searchId,
        int searchResultPosition,
        int maxResults,
        bool timeReverseOrder,
        CancellationToken ct)
    {
        var endpoint = BuildEndpoint(reloj);

        var body = new HikvisionAcsEventSearchRequestDto
        {
            AcsEventCond = new HikvisionAcsEventCondDto
            {
                SearchId = searchId,
                SearchResultPosition = searchResultPosition,
                MaxResults = maxResults,
                StartTime = FormatClockTime(fromUtc),
                EndTime = FormatClockTime(toUtc),
                TimeReverseOrder = timeReverseOrder,
                IsAttendanceInfo = true
            }
        };

        var payload = JsonSerializer.Serialize(body, JsonOptions);

        using var handler = BuildHandler();
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.HttpTimeoutSeconds))
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Poll ISAPI fallo para reloj {reloj.IdReloj}. Status={(int)response.StatusCode} Body={raw}");
        }

        var parsed = JsonSerializer.Deserialize<HikvisionAcsEventSearchResponseDto>(raw, JsonOptions);
        if (parsed?.AcsEvent == null)
        {
            throw new InvalidOperationException($"Respuesta AcsEvent invalida en reloj {reloj.IdReloj}");
        }

        return parsed.AcsEvent;
    }

    public async Task<DateTimeOffset?> GetOldestEventTimeAsync(
        Reloj reloj,
        DateTimeOffset bootstrapStartUtc,
        DateTimeOffset nowUtc,
        int maxResults,
        CancellationToken ct)
    {
        var result = await SearchAsync(
            reloj: reloj,
            fromUtc: bootstrapStartUtc,
            toUtc: nowUtc,
            searchId: Guid.NewGuid().ToString("N"),
            searchResultPosition: 0,
            maxResults: Math.Max(1, maxResults),
            timeReverseOrder: false,
            ct: ct);

        if (result.NumOfMatches <= 0 || result.InfoList.Count == 0)
        {
            return null;
        }

        var first = result.InfoList[0];
        if (string.IsNullOrWhiteSpace(first.Time) || !DateTimeOffset.TryParse(first.Time, out var parsed))
        {
            _logger.LogWarning("No se pudo parsear time en oldest query. relojId={RelojId}", reloj.IdReloj);
            return null;
        }

        return parsed.ToUniversalTime();
    }

    private static string FormatClockTime(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz");
    }

    private static HttpClientHandler BuildHandler()
    {
        var handler = new HttpClientHandler();

        var user = Environment.GetEnvironmentVariable("ISAPI_USER");
        var pass = Environment.GetEnvironmentVariable("ISAPI_PASSWORD");
        if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass))
        {
            handler.Credentials = new NetworkCredential(user, pass);
        }

        return handler;
    }

    private static string BuildEndpoint(Reloj reloj)
    {
        if (reloj.Residential == null || string.IsNullOrWhiteSpace(reloj.Residential.IpActual))
        {
            throw new ArgumentException($"Reloj {reloj.IdReloj} sin residential/ip para poll");
        }

        if (reloj.Puerto <= 0)
        {
            throw new ArgumentException($"Reloj {reloj.IdReloj} con puerto invalido para poll");
        }

        var scheme = (reloj.Puerto == 443 || reloj.Puerto == 8443) ? "https" : "http";
        return $"{scheme}://{reloj.Residential.IpActual}:{reloj.Puerto}/ISAPI/AccessControl/AcsEvent?format=json";
    }
}
