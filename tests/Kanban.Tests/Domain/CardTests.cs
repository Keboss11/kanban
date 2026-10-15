using Kanban.Domain.Boards;
using Kanban.Domain.Common;

namespace Kanban.Tests.Domain;

public class CardTests
{
    private static Card NewCard(IEnumerable<string>? labels = null, DateOnly? due = null)
    {
        var board = Board.CreateWithDefaultColumns("user-1", "Proyecto");
        return board.AddCard(board.Columns[0].Id, "Tarjeta", labels: labels, dueDate: due);
    }

    [Fact]
    public void Labels_are_trimmed_and_deduplicated_ignoring_case()
    {
        var card = NewCard([" bug ", "Bug", "", "frontend"]);

        Assert.Equal(["bug", "frontend"], card.Labels);
    }

    [Fact]
    public void Too_many_labels_are_rejected()
    {
        var labels = Enumerable.Range(0, Card.MaxLabels + 1).Select(i => $"l{i}");
        Assert.Throws<DomainException>(() => NewCard(labels));
    }

    [Fact]
    public void Title_is_required_and_trimmed()
    {
        var card = NewCard();

        card.Update("  Nuevo  ", null, Priority.High, null, null);
        Assert.Equal("Nuevo", card.Title);

        Assert.Throws<DomainException>(() => card.Update(" ", null, Priority.High, null, null));
    }

    [Fact]
    public void Invalid_priority_is_rejected()
    {
        var card = NewCard();
        Assert.Throws<DomainException>(() => card.Update("T", null, (Priority)42, null, null));
    }

    [Fact]
    public void IsOverdue_compares_with_today()
    {
        var today = new DateOnly(2026, 10, 15);

        Assert.True(NewCard(due: today.AddDays(-1)).IsOverdue(today));
        Assert.False(NewCard(due: today).IsOverdue(today));
        Assert.False(NewCard().IsOverdue(today));
    }

    [Fact]
    public void Comment_requires_text()
    {
        Assert.Throws<DomainException>(() => new Comment(Guid.NewGuid(), "user-1", "  "));
    }
}
