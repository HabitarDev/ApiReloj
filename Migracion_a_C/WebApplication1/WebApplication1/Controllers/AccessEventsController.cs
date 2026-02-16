using IServices.IAccesEvent;
using Microsoft.AspNetCore.Mvc;
using Models.Dominio;
using Models.WebApi;
using WebApplication1.Filters;

namespace WebApplication1.Controllers;

[ApiController]
[Route("[controller]")]
public class AccessEventsController(
    IAccesEventService accesEventService,
    ILogger<AccessEventsController> logger) : ControllerBase
{
    private readonly IAccesEventService _accesEventService = accesEventService;
    private readonly ILogger<AccessEventsController> _logger = logger;

    [HttpPost("push/{relojId:int}")]
    [ServiceFilter(typeof(AuthorizationPushFilter))]
    public async Task<ActionResult<PushIngestResultDto>> Push([FromRoute] int relojId)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? string.Empty;
        var contentType = Request.ContentType ?? string.Empty;

        var (eventPayload, hasPicture) = await ReadEventPayloadAsync(contentType);
        var envelope = new HikvisionPushEnvelopeDto
        {
            RelojId = relojId,
            RemoteIp = remoteIp,
            ContentType = contentType,
            EventPayloadRaw = eventPayload,
            HasPicture = hasPicture
        };

        if (!HttpContext.Items.TryGetValue(PushAuthContext.HttpContextItemKey, out var contextRaw)
            || contextRaw is not PushAuthContext authContext)
        {
            throw new InvalidOperationException("No se pudo resolver el contexto de autorizacion push");
        }

        var result = _accesEventService.ProcesarPush(envelope, authContext);
        _logger.LogInformation(
            "Push endpoint result: status={Status}, reason={Reason}, eventType={EventType}, serialNo={SerialNo}, deviceSn={DeviceSn}, relojId={RelojId}, remoteIp={RemoteIp}, contentType={ContentType}",
            result.Status, result.Reason, result.EventType, result.SerialNo, result.DeviceSn, relojId, remoteIp, contentType);
        return Ok(result);
    }

    [HttpGet]
    public ActionResult<List<AccesEventDto>> Get()
    {
        return Ok(_accesEventService.ListarTodos());
    }

    private async Task<(string payload, bool hasPicture)> ReadEventPayloadAsync(string contentType)
    {
        if (IsMultipart(contentType))
        {
            var form = await Request.ReadFormAsync();
            var payload = TryGetEventPart(form);
            var hasPicture = form.Files.Count > 0;

            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentException("No se encontro el payload del evento en multipart/form-data");
            }

            return (payload, hasPicture);
        }

        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("El body del push esta vacio");
        }

        return (raw, false);
    }

    private static bool IsMultipart(string contentType)
    {
        return contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetEventPart(IFormCollection form)
    {
        string[] preferredKeys = ["Event_Type", "event_type", "eventType", "EventType", "event"];
        foreach (var key in preferredKeys)
        {
            if (form.TryGetValue(key, out var value))
            {
                var candidate = value.ToString();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }
        }

        foreach (var pair in form)
        {
            var candidate = pair.Value.ToString();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
