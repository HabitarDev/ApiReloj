using Dominio;
using IServices.IJornada;
using Models.Dominio;

namespace Service.JornadaServicess;

public class JornadaEntityService : IJornadaEntityService
{
    public JornadaDto FromEntity(Jornada jornada)
    {
        return new JornadaDto
        {
            JornadaId = jornada.JornadaId,
            EmployeeNumber = jornada.EmployeeNumber,
            ClockSn = jornada.ClockSn,
            StartAt = jornada.StartAt,
            BreakInAt = jornada.BreakInAt,
            BreakOutAt = jornada.BreakOutAt,
            EndAt = jornada.EndAt,
            StatusCheck = jornada.StatusCheck,
            StatusBreak = jornada.StatusBreak,
            UpdatedAt = jornada.UpdatedAt
        };
    }
}
