using Kanban.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Kanban.Api.Errors;

/// <summary>
/// Traduce las excepciones de negocio a ProblemDetails. Un recurso ajeno responde 404
/// igual que uno inexistente, para no revelar que existe.
/// </summary>
public sealed class KanbanExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "No encontrado"),
            DomainException => (StatusCodes.Status400BadRequest, "Petición no válida"),
            // JSON mal formado o cuerpo ausente. En Development se lanza como excepción en vez de responder 400.
            BadHttpRequestException bad => (bad.StatusCode, "Petición no válida"),
            _ => (0, null),
        };
        if (status == 0)
            return false;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = exception.Message },
        });
    }
}
