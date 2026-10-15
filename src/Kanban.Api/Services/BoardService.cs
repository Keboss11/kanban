using Kanban.Domain.Activity;
using Kanban.Domain.Boards;
using Kanban.Shared.Boards;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services;

/// <summary>Tableros, columnas e historial.</summary>
public sealed class BoardService(BoardAccess access)
{
    private readonly Infrastructure.Persistence.KanbanDbContext _db = access.Db;

    public async Task<IReadOnlyList<BoardSummaryDto>> ListAsync(CancellationToken ct = default) =>
        await access.OwnedBoards
            .OrderBy(b => b.CreatedAt)
            .Select(b => new BoardSummaryDto(
                b.Id, b.Name, b.Description,
                _db.Cards.Count(c => _db.Columns.Any(col => col.Id == c.ColumnId && col.BoardId == b.Id)),
                b.CreatedAt))
            .ToListAsync(ct);

    public async Task<BoardDto> GetAsync(Guid boardId, CancellationToken ct = default) =>
        (await access.LoadAsync(boardId, ct)).ToDto();

    public async Task<BoardDto> CreateAsync(CreateBoardRequest request, CancellationToken ct = default)
    {
        var board = request.WithDefaultColumns
            ? Board.CreateWithDefaultColumns(access.User.Id, request.Name, request.Description)
            : new Board(access.User.Id, request.Name, request.Description);

        _db.Boards.Add(board);
        access.Record(board.Id, ActivityAction.BoardCreated, $"creó el tablero «{board.Name}»");
        await access.SaveAsync(ct);
        return board.ToDto();
    }

    public async Task<BoardDto> UpdateAsync(Guid boardId, UpdateBoardRequest request, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        board.Update(request.Name, request.Description);
        access.Record(board.Id, ActivityAction.BoardUpdated, $"actualizó el tablero «{board.Name}»");
        await access.SaveAsync(ct);
        return board.ToDto();
    }

    public async Task DeleteAsync(Guid boardId, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        _db.Boards.Remove(board);
        await access.SaveAsync(ct);
        await access.NotifyDeletedAsync(boardId);
    }

    // ---- Columnas ----

    public async Task<ColumnDto> AddColumnAsync(Guid boardId, CreateColumnRequest request, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        var column = board.AddColumn(request.Name, request.WipLimit);
        access.Record(board.Id, ActivityAction.ColumnCreated, $"añadió la columna «{column.Name}»");
        await access.SaveAsync(ct);
        return column.ToDto();
    }

    public async Task<ColumnDto> UpdateColumnAsync(Guid boardId, Guid columnId, UpdateColumnRequest request, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        var column = board.GetColumn(columnId);
        column.Rename(request.Name);
        column.SetWipLimit(request.WipLimit);
        access.Record(board.Id, ActivityAction.ColumnUpdated, $"editó la columna «{column.Name}»");
        await access.SaveAsync(ct);
        return column.ToDto();
    }

    public async Task<BoardDto> MoveColumnAsync(Guid boardId, Guid columnId, MoveColumnRequest request, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        board.MoveColumn(columnId, request.Index);
        access.Record(board.Id, ActivityAction.ColumnMoved, $"movió la columna «{board.GetColumn(columnId).Name}»");
        await access.SaveAsync(ct);
        return board.ToDto();
    }

    public async Task DeleteColumnAsync(Guid boardId, Guid columnId, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        var name = board.GetColumn(columnId).Name;
        board.RemoveColumn(columnId);
        access.Record(board.Id, ActivityAction.ColumnDeleted, $"borró la columna «{name}»");
        await access.SaveAsync(ct);
    }

    // ---- Historial ----

    public async Task<IReadOnlyList<ActivityDto>> GetActivityAsync(Guid boardId, int take = 50, CancellationToken ct = default)
    {
        if (!await access.OwnedBoards.AnyAsync(b => b.Id == boardId, ct))
            throw new Domain.Common.EntityNotFoundException("El tablero no existe.");

        var rows = await (
                from a in _db.ActivityLogs
                join u in _db.Users on a.UserId equals u.Id
                where a.BoardId == boardId
                orderby a.CreatedAt descending
                select new { a, Name = u.DisplayName ?? u.Email! })
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

        return rows
            .Select(r => new ActivityDto(r.a.Id, r.a.CardId, r.Name, r.a.Source.ToDto(), r.a.Action.ToString(), r.a.Summary, r.a.CreatedAt))
            .ToList();
    }
}
