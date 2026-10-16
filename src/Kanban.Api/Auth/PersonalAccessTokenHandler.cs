using System.Security.Claims;
using System.Text.Encodings.Web;
using Kanban.Infrastructure.Identity;
using Kanban.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Api.Auth;

/// <summary>
/// Autenticación con token personal (<c>Authorization: Bearer kbn_…</c>) para el endpoint MCP.
/// Todo lo que se haga con estos tokens queda marcado como hecho por IA.
/// </summary>
public sealed class PersonalAccessTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    KanbanDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PersonalAccessToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        const string bearer = "Bearer ";
        if (!header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var value = header[bearer.Length..].Trim();
        if (!value.StartsWith(PersonalAccessToken.ValuePrefix, StringComparison.Ordinal))
            return AuthenticateResult.NoResult();

        var hash = PersonalAccessToken.Hash(value);
        var token = await db.PersonalAccessTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, Context.RequestAborted);
        if (token is null)
            return AuthenticateResult.Fail("Token no válido.");

        if (token.MarkUsed(DateTime.UtcNow))
            await db.SaveChangesAsync(Context.RequestAborted);

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, token.UserId),
            new Claim(HttpCurrentUser.SourceClaim, "ai"),
            new Claim("kanban:token_id", token.Id.ToString()),
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }
}
