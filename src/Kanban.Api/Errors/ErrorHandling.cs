using Kanban.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace Kanban.Api.Errors;

/// <summary>Recurso inexistente o ajeno: se responde 404 en ambos casos para no revelar que existe.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Traduce las excepciones de negocio a ProblemDetails (400 / 404).</summary>
public sealed class KanbanExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "Petición no válida"),
            NotFoundException => (StatusCodes.Status404NotFound, "No encontrado"),
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
