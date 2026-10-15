using Kanban.Shared.Boards;

namespace Kanban.Web.Models;

/// <summary>Copia mutable del tablero para aplicar cambios optimistas antes de que responda la API.</summary>
public sealed class BoardState
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required List<ColumnState> Columns { get; init; }

    public static BoardState From(BoardDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        Columns = dto.Columns.Select(ColumnState.From).ToList(),
    };

    public ColumnState? ColumnOf(Guid cardId) => Columns.FirstOrDefault(c => c.Cards.Any(x => x.Id == cardId));

    public CardDto? FindCard(Guid cardId) => Columns.SelectMany(c => c.Cards).FirstOrDefault(c => c.Id == cardId);

    /// <summary>Mueve la tarjeta localmente. <paramref name="index"/> es la posición en la lista final.</summary>
    public void MoveCard(Guid cardId, Guid targetColumnId, int index)
    {
        var source = ColumnOf(cardId);
        var target = Columns.FirstOrDefault(c => c.Id == targetColumnId);
        if (source is null || target is null)
            return;

        var card = source.Cards.First(c => c.Id == cardId);
        source.Cards.Remove(card);
        target.Cards.Insert(Math.Clamp(index, 0, target.Cards.Count), card with { ColumnId = targetColumnId });
    }

    public void ReplaceCard(CardDto card)
    {
        var column = ColumnOf(card.Id);
        if (column is null)
            return;
        var i = column.Cards.FindIndex(c => c.Id == card.Id);
        column.Cards[i] = card;
    }

    public void RemoveCard(Guid cardId) => ColumnOf(cardId)?.Cards.RemoveAll(c => c.Id == cardId);
}

public sealed class ColumnState
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public int? WipLimit { get; set; }
    public required List<CardDto> Cards { get; init; }

    public bool IsAtWipLimit => WipLimit is { } limit && Cards.Count >= limit;

    public static ColumnState From(ColumnDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        WipLimit = dto.WipLimit,
        Cards = dto.Cards.ToList(),
    };
}
