using Dominio;
using IServices.IJornada;
using Models.Dominio;
using System.Text.Json;

namespace Service.JornadaServicess;

public class JornadaEntityService : IJornadaEntityService
{
    public JornadaDto FromEntity(Jornada jornada)
    {
        return new JornadaDto
        {
            JornadaId = jornada.JornadaId,
            EmployeeNumber = jornada.EmployeeNumber,
            ResidentialId = jornada.ResidentialId,
            ClockSn = jornada.ClockSn,
            StartAt = jornada.StartAt,
            BreakInAt = jornada.BreakInAt,
            BreakOutAt = jornada.BreakOutAt,
            EndAt = jornada.EndAt,
            StatusCheck = jornada.StatusCheck,
            StatusBreak = jornada.StatusBreak,
            Warnings = DeserializeCodes(jornada.WarningsJson),
            Errors = DeserializeCodes(jornada.ErrorsJson),
            ProjectionStatus = jornada.ProjectionStatus,
            Revision = jornada.Revision,
            IsDeleted = jornada.IsDeleted,
            StartDeviceSn = jornada.StartDeviceSn,
            StartSerialNumber = jornada.StartSerialNumber,
            BreakInDeviceSn = jornada.BreakInDeviceSn,
            BreakInSerialNumber = jornada.BreakInSerialNumber,
            BreakOutDeviceSn = jornada.BreakOutDeviceSn,
            BreakOutSerialNumber = jornada.BreakOutSerialNumber,
            EndDeviceSn = jornada.EndDeviceSn,
            EndSerialNumber = jornada.EndSerialNumber,
            CreatedAt = jornada.CreatedAt,
            UpdatedAt = jornada.UpdatedAt
        };
    }

    private static List<string> DeserializeCodes(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
