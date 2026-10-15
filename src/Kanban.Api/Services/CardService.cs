using Kanban.Domain.Activity;
using Kanban.Domain.Boards;
using Kanban.Shared.Boards;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services;

/// <summary>Tarjetas, comentarios y búsqueda.</summary>
public sealed class CardService(BoardAccess access)
{
    public const int SearchLimit = 50;

    private readonly Infrastructure.Persistence.KanbanDbContext _db = access.Db;

    public async Task<CardDto> GetAsync(Guid boardId, Guid cardId, CancellationToken ct = default) =>
        (await access.LoadAsync(boardId, ct)).GetCard(cardId).ToDto();

    public async Task<CardDto> CreateAsync(Guid boardId, CreateCardRequest request, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        var card = board.AddCard(request.ColumnId, request.Title, request.Description,
            request.Priority.ToDomain(), request.Labels, request.DueDate);
        access.Record(board.Id, ActivityAction.CardCreated,
            $"creó «{card.Title}» en {board.GetColumn(request.ColumnId).Name}", card.Id);
        await access.SaveAsync(ct);
        return card.ToDto();
    }

    public async Task<CardDto> UpdateAsync(Guid boardId, Guid cardId, UpdateCardRequest request, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        var card = board.GetCard(cardId);
        card.Update(request.Title, request.Description, request.Priority.ToDomain(), request.Labels, request.DueDate);
        access.Record(board.Id, ActivityAction.CardUpdated, $"editó «{card.Title}»", card.Id);
        await access.SaveAsync(ct);
        return card.ToDto();
    }

    public async Task<CardDto> MoveAsync(Guid boardId, Guid cardId, MoveCardRequest request, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        var source = board.GetColumnOf(cardId);
        var target = board.MoveCard(cardId, request.ColumnId, request.Index);
        var card = board.GetCard(cardId);

        var summary = source == target
            ? $"reordenó «{card.Title}» en {target.Name}"
            : $"movió «{card.Title}» a {target.Name}";
        access.Record(board.Id, ActivityAction.CardMoved, summary, card.Id);
        await access.SaveAsync(ct);
        return card.ToDto();
    }

    public async Task DeleteAsync(Guid boardId, Guid cardId, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        var card = board.RemoveCard(cardId);
        access.Record(board.Id, ActivityAction.CardDeleted, $"borró «{card.Title}»", card.Id);
        await access.SaveAsync(ct);
    }

    // ---- Comentarios ----

    public async Task<IReadOnlyList<CommentDto>> ListCommentsAsync(Guid boardId, Guid cardId, CancellationToken ct = default)
    {
        (await access.LoadAsync(boardId, ct)).GetCard(cardId);

        return await (
                from c in _db.Comments
                join u in _db.Users on c.AuthorId equals u.Id
                where c.CardId == cardId
                orderby c.CreatedAt
                select new CommentDto(c.Id, c.CardId, u.DisplayName ?? u.Email!, c.Text, c.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<CommentDto> AddCommentAsync(Guid boardId, Guid cardId, CreateCommentRequest request, CancellationToken ct = default)
    {
        var board = await access.LoadAsync(boardId, ct);
        var card = board.GetCard(cardId);
        var comment = new Comment(card.Id, access.User.Id, request.Text);
        _db.Comments.Add(comment);
        access.Record(board.Id, ActivityAction.CommentAdded, $"comentó en «{card.Title}»", card.Id);
        await access.SaveAsync(ct);

        var author = await _db.Users.Where(u => u.Id == access.User.Id).Select(u => u.DisplayName ?? u.Email!).SingleAsync(ct);
        return new CommentDto(comment.Id, comment.CardId, author, comment.Text, comment.CreatedAt);
    }

    // ---- Búsqueda ----

    /// <summary>Busca en todos los tableros del usuario (o en uno). Texto en título y descripción.</summary>
    public async Task<IReadOnlyList<CardSearchResultDto>> SearchAsync(CardSearchQuery query, CancellationToken ct = default)
    {
        var rows =
            from card in _db.Cards
            join column in _db.Columns on card.ColumnId equals column.Id
            join board in access.OwnedBoards on column.BoardId equals board.Id
            select new { card, column, board };

        if (query.BoardId is { } boardId)
            rows = rows.Where(r => r.board.Id == boardId);

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            var text = query.Text.Trim().ToLower();
            rows = rows.Where(r => r.card.Title.ToLower().Contains(text)
                || (r.card.Description != null && r.card.Description.ToLower().Contains(text)));
        }

        if (query.Priority is { } priority)
        {
            var p = priority.ToDomain();
            rows = rows.Where(r => r.card.Priority == p);
        }

        var results = await rows
            .OrderBy(r => r.board.CreatedAt).ThenBy(r => r.column.Position).ThenBy(r => r.card.Position)
            .Select(r => new { r.card, BoardId = r.board.Id, BoardName = r.board.Name, ColumnName = r.column.Name })
            .ToListAsync(ct);

        // Las etiquetas se guardan como JSON; se filtran en memoria para ignorar mayúsculas en cualquier proveedor.
        if (!string.IsNullOrWhiteSpace(query.Label))
        {
            var label = query.Label.Trim();
            results = results.Where(r => r.card.Labels.Contains(label, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        return results
            .Take(SearchLimit)
            .Select(r => new CardSearchResultDto(r.BoardId, r.BoardName, r.ColumnName, r.card.ToDto()))
            .ToList();
    }
}
