using Kanban.Domain.Common;

namespace Kanban.Domain.Boards;

/// <summary>Raíz del agregado: toda modificación de columnas y tarjetas pasa por aquí.</summary>
public sealed class Board
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 2000;

    public static readonly string[] DefaultColumns = ["Por hacer", "En curso", "Hecho"];

    private readonly List<Column> _columns = [];

    private Board()
    {
        OwnerId = null!;
        Name = null!;
    }

    public Board(string ownerId, string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new DomainException("El tablero necesita un propietario.");

        OwnerId = ownerId;
        Name = Guard.Required(name, NameMaxLength, "El nombre del tablero");
        Description = Guard.Optional(description, DescriptionMaxLength, "La descripción");
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string OwnerId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<Column> Columns => _columns.OrderBy(c => c.Position).ToList();

    public static Board CreateWithDefaultColumns(string ownerId, string name, string? description = null)
    {
        var board = new Board(ownerId, name, description);
        foreach (var column in DefaultColumns)
            board.AddColumn(column);
        return board;
    }

    public void Update(string name, string? description)
    {
        Name = Guard.Required(name, NameMaxLength, "El nombre del tablero");
        Description = Guard.Optional(description, DescriptionMaxLength, "La descripción");
    }

    public bool IsOwnedBy(string userId) => OwnerId == userId;

    // ---- Columnas ----

    public Column AddColumn(string name, int? wipLimit = null)
    {
        var last = _columns.Count == 0 ? (double?)null : _columns.Max(c => c.Position);
        var column = new Column(Id, name, Positioning.Between(last, null), wipLimit);
        _columns.Add(column);
        return column;
    }

    public Column GetColumn(Guid columnId) =>
        _columns.FirstOrDefault(c => c.Id == columnId)
        ?? throw new DomainException("La columna no existe en este tablero.");

    public void MoveColumn(Guid columnId, int targetIndex)
    {
        var column = GetColumn(columnId);
        var others = Columns.Where(c => c != column).ToList();
        column.Position = Positioning.At(others.Select(c => c.Position).ToList(), targetIndex, () =>
        {
            for (var i = 0; i < others.Count; i++)
                others[i].Position = (i + 1) * Positioning.Step;
        });
    }

    public void RemoveColumn(Guid columnId)
    {
        var column = GetColumn(columnId);
        if (column.Cards.Count > 0)
            throw new DomainException("No se puede borrar una columna que tiene tarjetas.");
        _columns.Remove(column);
    }

    // ---- Tarjetas ----

    public Card AddCard(Guid columnId, string title, string? description = null,
        Priority priority = Priority.Medium, IEnumerable<string>? labels = null, DateOnly? dueDate = null)
    {
        var column = GetColumn(columnId);
        column.EnsureCanAcceptCard();
        var card = new Card(title, description, priority, labels, dueDate);
        column.Insert(card, int.MaxValue);
        return card;
    }

    public Card GetCard(Guid cardId) => FindCard(cardId).Card;

    public Column GetColumnOf(Guid cardId) => FindCard(cardId).Column;

    /// <summary>Mueve la tarjeta a <paramref name="targetIndex"/> de la columna destino (se recorta al rango válido).</summary>
    public Column MoveCard(Guid cardId, Guid targetColumnId, int targetIndex)
    {
        var (source, card) = FindCard(cardId);
        var target = GetColumn(targetColumnId);
        if (target != source)
            target.EnsureCanAcceptCard();

        source.Detach(card);
        target.Insert(card, targetIndex);
        card.Touch();
        return target;
    }

    public Card RemoveCard(Guid cardId)
    {
        var (column, card) = FindCard(cardId);
        column.Detach(card);
        return card;
    }

    private (Column Column, Card Card) FindCard(Guid cardId)
    {
        foreach (var column in _columns)
        {
            var card = column.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card is not null)
                return (column, card);
        }
        throw new DomainException("La tarjeta no existe en este tablero.");
    }
}
