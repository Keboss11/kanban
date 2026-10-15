using Kanban.Api.Services;
using Kanban.Shared.Boards;

namespace Kanban.Api.Endpoints;

public static class BoardEndpoints
{
    public static IEndpointRouteBuilder MapBoardEndpoints(this IEndpointRouteBuilder app)
    {
        var boards = app.MapGroup("/api/boards").WithTags("Boards").RequireAuthorization();

        boards.MapGet("/", (BoardService s, CancellationToken ct) => s.ListAsync(ct));

        boards.MapPost("/", async (CreateBoardRequest req, BoardService s, CancellationToken ct) =>
        {
            var board = await s.CreateAsync(req, ct);
            return Results.Created($"/api/boards/{board.Id}", board);
        });

        boards.MapGet("/{boardId:guid}", (Guid boardId, BoardService s, CancellationToken ct) => s.GetAsync(boardId, ct));

        boards.MapPut("/{boardId:guid}", (Guid boardId, UpdateBoardRequest req, BoardService s, CancellationToken ct) =>
            s.UpdateAsync(boardId, req, ct));

        boards.MapDelete("/{boardId:guid}", async (Guid boardId, BoardService s, CancellationToken ct) =>
        {
            await s.DeleteAsync(boardId, ct);
            return Results.NoContent();
        });

        boards.MapGet("/{boardId:guid}/activity", (Guid boardId, int? take, BoardService s, CancellationToken ct) =>
            s.GetActivityAsync(boardId, take ?? 50, ct));

        // ---- Columnas ----
        var columns = boards.MapGroup("/{boardId:guid}/columns").WithTags("Columns");

        columns.MapPost("/", async (Guid boardId, CreateColumnRequest req, BoardService s, CancellationToken ct) =>
        {
            var column = await s.AddColumnAsync(boardId, req, ct);
            return Results.Created($"/api/boards/{boardId}/columns/{column.Id}", column);
        });

        columns.MapPut("/{columnId:guid}", (Guid boardId, Guid columnId, UpdateColumnRequest req, BoardService s, CancellationToken ct) =>
            s.UpdateColumnAsync(boardId, columnId, req, ct));

        columns.MapPost("/{columnId:guid}/move", (Guid boardId, Guid columnId, MoveColumnRequest req, BoardService s, CancellationToken ct) =>
            s.MoveColumnAsync(boardId, columnId, req, ct));

        columns.MapDelete("/{columnId:guid}", async (Guid boardId, Guid columnId, BoardService s, CancellationToken ct) =>
        {
            await s.DeleteColumnAsync(boardId, columnId, ct);
            return Results.NoContent();
        });

        return app;
    }
}
