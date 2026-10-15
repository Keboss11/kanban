using System.Security.Claims;
using Kanban.Domain.Activity;

namespace Kanban.Api.Auth;

/// <summary>Usuario autenticado de la petición actual y el origen del cambio (web o IA).</summary>
public interface ICurrentUser
{
    string Id { get; }
    ActivitySource Source { get; }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    /// <summary>Claim que añaden los tokens del MCP para marcar los cambios como hechos por IA.</summary>
    public const string SourceClaim = "kanban:source";

    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("No hay petición HTTP activa.");

    public string Id =>
        Principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

    public ActivitySource Source =>
        Principal.FindFirstValue(SourceClaim) == "ai" ? ActivitySource.Ai : ActivitySource.Web;
}
