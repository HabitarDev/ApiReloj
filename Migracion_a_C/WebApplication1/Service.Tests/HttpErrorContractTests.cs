using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using WebApplication1.Filters;
using Xunit;

namespace Service.Tests;

public class HttpErrorContractTests
{
    [Fact]
    public void MissingEntity_MapsToNotFoundProblemDetails()
    {
        var result = Execute(new KeyNotFoundException("El Device no existe"));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("El Device no existe", problem.Detail);
    }

    [Fact]
    public void DuplicateEntity_MapsToConflictProblemDetails()
    {
        var result = Execute(new InvalidOperationException("El Device ya existe"));

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ProblemDetails>(result.Value).Status);
    }

    [Fact]
    public void InvalidInput_MapsToBadRequestProblemDetails()
    {
        var result = Execute(new ArgumentException("Puerto de reloj invalido"));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemDetails>(result.Value).Status);
    }

    [Fact]
    public void BusinessRule_MapsToUnprocessableEntityProblemDetails()
    {
        var result = Execute(new InvalidOperationException("No se pudo completar la operacion"));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, Assert.IsType<ProblemDetails>(result.Value).Status);
    }

    [Fact]
    public void UnexpectedError_DoesNotExposeItsDetail()
    {
        var result = Execute(new Exception("database-password-must-not-leak"));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.DoesNotContain("password", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private static ObjectResult Execute(Exception exception)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());
        var context = new ExceptionContext(actionContext, [])
        {
            Exception = exception
        };

        new GlobalExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        return Assert.IsType<ObjectResult>(context.Result);
    }
}
