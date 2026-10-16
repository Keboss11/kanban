using Kanban.Domain.Common;
using Kanban.Infrastructure.Identity;
using Kanban.Infrastructure.Persistence;
using Kanban.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Auth;

/// <summary>Gestión de tokens personales desde la web (con la sesión normal, no con otro token).</summary>
public static class TokenEndpoints
{
    public const int MaxTokensPerUser = 10;

    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder app)
    {
        var tokens = app.MapGroup("/api/tokens").WithTags("Tokens").RequireAuthorization();

        tokens.MapGet("/", async (ICurrentUser user, KanbanDbContext db, CancellationToken ct) =>
            await db.PersonalAccessTokens
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new PersonalAccessTokenDto(t.Id, t.Name, t.DisplayPrefix, t.CreatedAt, t.LastUsedAt))
                .ToListAsync(ct));

        tokens.MapPost("/", async (CreateTokenRequest req, ICurrentUser user, KanbanDbContext db, CancellationToken ct) =>
        {
            var name = req.Name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > PersonalAccessToken.NameMaxLength)
                throw new DomainException($"El nombre es obligatorio y como máximo de {PersonalAccessToken.NameMaxLength} caracteres.");
            if (await db.PersonalAccessTokens.CountAsync(t => t.UserId == user.Id, ct) >= MaxTokensPerUser)
                throw new DomainException($"Como máximo {MaxTokensPerUser} tokens. Revoca alguno antes de crear otro.");

            var (token, value) = PersonalAccessToken.Create(user.Id, name);
            db.PersonalAccessTokens.Add(token);
            await db.SaveChangesAsync(ct);

            var dto = new PersonalAccessTokenDto(token.Id, token.Name, token.DisplayPrefix, token.CreatedAt, token.LastUsedAt);
            return Results.Created($"/api/tokens/{token.Id}", new CreatedTokenDto(dto, value));
        });

        tokens.MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, KanbanDbContext db, CancellationToken ct) =>
        {
            var deleted = await db.PersonalAccessTokens.Where(t => t.Id == id && t.UserId == user.Id).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        });

        return app;
    }
}
