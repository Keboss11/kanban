using Kanban.Domain.Common;

namespace Kanban.Domain.Boards;

public sealed class Column
{
    public const int NameMaxLength = 50;

    private readonly List<Card> _cards = [];

    private Column()
    {
        Name = null!;
    }

    internal Column(Guid boardId, string name, double position, int? wipLimit)
    {
        BoardId = boardId;
        Name = Guard.Required(name, NameMaxLength, "El nombre de la columna");
        Position = position;
        SetWipLimit(wipLimit);
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid BoardId { get; private set; }
    public string Name { get; private set; }
    public double Position { get; internal set; }
    public int? WipLimit { get; private set; }

    public IReadOnlyList<Card> Cards => _cards.OrderBy(c => c.Position).ToList();

    public bool IsAtWipLimit => WipLimit is { } limit && _cards.Count >= limit;

    public void Rename(string name) =>
        Name = Guard.Required(name, NameMaxLength, "El nombre de la columna");

    /// <summary>
    /// Bajar el límite por debajo del número actual de tarjetas está permitido:
    /// la columna queda por encima del límite y solo se bloquean nuevas entradas.
    /// </summary>
    public void SetWipLimit(int? wipLimit)
    {
        if (wipLimit is < 1)
            throw new DomainException("El límite WIP debe ser al menos 1.");
        WipLimit = wipLimit;
    }

    internal void EnsureCanAcceptCard()
    {
        if (IsAtWipLimit)
            throw new DomainException($"La columna «{Name}» ha alcanzado su límite WIP ({WipLimit}).");
    }

    internal void Insert(Card card, int index)
    {
        var ordered = Cards;
        var position = Positioning.At(ordered.Select(c => c.Position).ToList(), index, () =>
        {
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].Position = (i + 1) * Positioning.Step;
        });
        card.PlaceIn(Id, position);
        _cards.Add(card);
    }

    internal void Detach(Card card) => _cards.Remove(card);
}
