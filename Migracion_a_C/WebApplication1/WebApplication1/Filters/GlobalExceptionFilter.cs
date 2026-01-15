using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebApplication1.Filters;

public sealed class GlobalExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var ex = context.Exception;

        ObjectResult result = ex switch
        {
            UnauthorizedAccessException uae => PD(401, "No autorizado", uae.Message),
            KeyNotFoundException knf => PD(404, "No encontrado", knf.Message),

            InvalidOperationException ioe when IsConflictMessage(ioe.Message)
                => PD(409, "Conflicto", ioe.Message),
            ArgumentException ae when IsConflictMessage(ae.Message)
                => PD(409, "Conflicto", ae.Message),

            InvalidOperationException ioe => PD(422, "Regla de negocio", ioe.Message),

            ArgumentException ae when IsNotFoundMessage(ae.Message)
                => PD(404, "No encontrado", ae.Message),

            ArgumentException ae => PD(400, "Argumento inválido", ae.Message),

            _ => PD(500, "Error interno", "Ocurrió un error inesperado")
        };

        context.Result = result;
        context.ExceptionHandled = true;
    }

    private static bool IsNotFoundMessage(string? message)
    {
        if(string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var m = message.Trim().ToLowerInvariant();
        return m.StartsWith("no existe")
               || m.StartsWith("no se encontró")
               || m.StartsWith("no se encontro")
               || m.Contains("inexistente");
    }

    private static ObjectResult PD(int status, string title, string detail)
    {
        var pd = new ProblemDetails { Status = status, Title = title, Detail = detail };
        return new ObjectResult(pd) { StatusCode = status };
    }

    private static bool IsConflictMessage(string? message)
    {
        if(string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var m = message.Trim().ToLowerInvariant();
        return m.Contains("ya existe")
               || m.Contains("existente")
               || m.Contains("duplicado");
    }
}
