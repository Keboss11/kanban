using Kanban.Api.Services;
using Kanban.Shared.Boards;

namespace Kanban.Api.Endpoints;

public static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        var cards = app.MapGroup("/api/boards/{boardId:guid}/cards").WithTags("Cards").RequireAuthorization();

        cards.MapPost("/", async (Guid boardId, CreateCardRequest req, CardService s, CancellationToken ct) =>
        {
            var card = await s.CreateAsync(boardId, req, ct);
            return Results.Created($"/api/boards/{boardId}/cards/{card.Id}", card);
        });

        cards.MapGet("/{cardId:guid}", (Guid boardId, Guid cardId, CardService s, CancellationToken ct) =>
            s.GetAsync(boardId, cardId, ct));

        cards.MapPut("/{cardId:guid}", (Guid boardId, Guid cardId, UpdateCardRequest req, CardService s, CancellationToken ct) =>
            s.UpdateAsync(boardId, cardId, req, ct));

        cards.MapPost("/{cardId:guid}/move", (Guid boardId, Guid cardId, MoveCardRequest req, CardService s, CancellationToken ct) =>
            s.MoveAsync(boardId, cardId, req, ct));

        cards.MapDelete("/{cardId:guid}", async (Guid boardId, Guid cardId, CardService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(boardId, cardId, ct);
            return Results.NoContent();
        });

        cards.MapGet("/{cardId:guid}/comments", (Guid boardId, Guid cardId, CardService s, CancellationToken ct) =>
            s.ListCommentsAsync(boardId, cardId, ct));

        cards.MapPost("/{cardId:guid}/comments", async (Guid boardId, Guid cardId, CreateCommentRequest req, CardService s, CancellationToken ct) =>
        {
            var comment = await s.AddCommentAsync(boardId, cardId, req, ct);
            return Results.Created($"/api/boards/{boardId}/cards/{cardId}/comments/{comment.Id}", comment);
        });

        app.MapGet("/api/cards/search", (string? text, string? label, CardPriority? priority, Guid? boardId, CardService s, CancellationToken ct) =>
                s.SearchAsync(new CardSearchQuery(text, label, priority, boardId), ct))
            .WithTags("Cards")
            .RequireAuthorization();

        return app;
    }
}
