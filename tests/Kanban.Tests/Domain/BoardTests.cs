using Kanban.Domain.Boards;
using Kanban.Domain.Common;

namespace Kanban.Tests.Domain;

public class BoardTests
{
    private static Board NewBoard() => Board.CreateWithDefaultColumns("user-1", "Proyecto");

    private static string[] Titles(Column column) => column.Cards.Select(c => c.Title).ToArray();

    [Fact]
    public void CreateWithDefaultColumns_creates_three_ordered_columns()
    {
        var board = NewBoard();

        Assert.Equal(Board.DefaultColumns, board.Columns.Select(c => c.Name));
        Assert.All(board.Columns, c => Assert.Equal(board.Id, c.BoardId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Board_requires_name(string name)
    {
        Assert.Throws<DomainException>(() => new Board("user-1", name));
    }

    [Fact]
    public void AddCard_appends_to_end_of_column()
    {
        var board = NewBoard();
        var todo = board.Columns[0];

        board.AddCard(todo.Id, "A");
        board.AddCard(todo.Id, "B");
        board.AddCard(todo.Id, "C");

        Assert.Equal(["A", "B", "C"], Titles(todo));
        Assert.All(todo.Cards, c => Assert.Equal(todo.Id, c.ColumnId));
    }

    [Fact]
    public void MoveCard_to_other_column_at_index()
    {
        var board = NewBoard();
        var (todo, doing) = (board.Columns[0], board.Columns[1]);
        var a = board.AddCard(todo.Id, "A");
        board.AddCard(doing.Id, "X");
        board.AddCard(doing.Id, "Y");

        board.MoveCard(a.Id, doing.Id, 1);

        Assert.Empty(todo.Cards);
        Assert.Equal(["X", "A", "Y"], Titles(doing));
        Assert.Equal(doing.Id, a.ColumnId);
    }

    [Fact]
    public void MoveCard_within_same_column_only_changes_moved_card()
    {
        var board = NewBoard();
        var todo = board.Columns[0];
        var a = board.AddCard(todo.Id, "A");
        var b = board.AddCard(todo.Id, "B");
        var c = board.AddCard(todo.Id, "C");
        var (posA, posB) = (a.Position, b.Position);

        board.MoveCard(c.Id, todo.Id, 0);

        Assert.Equal(["C", "A", "B"], Titles(todo));
        Assert.Equal(posA, a.Position);
        Assert.Equal(posB, b.Position);
    }

    [Fact]
    public void MoveCard_index_out_of_range_is_clamped()
    {
        var board = NewBoard();
        var todo = board.Columns[0];
        var a = board.AddCard(todo.Id, "A");
        board.AddCard(todo.Id, "B");

        board.MoveCard(a.Id, todo.Id, 99);
        Assert.Equal(["B", "A"], Titles(todo));

        board.MoveCard(a.Id, todo.Id, -5);
        Assert.Equal(["A", "B"], Titles(todo));
    }

    [Fact]
    public void Repeated_inserts_at_same_spot_rebalance_and_keep_order()
    {
        var board = NewBoard();
        var todo = board.Columns[0];
        board.AddCard(todo.Id, "first");
        board.AddCard(todo.Id, "last");

        // Insertar siempre en el índice 1 parte el mismo hueco por la mitad hasta agotarlo.
        for (var i = 0; i < 100; i++)
        {
            var card = board.AddCard(todo.Id, $"n{i}");
            board.MoveCard(card.Id, todo.Id, 1);
        }

        var titles = Titles(todo);
        Assert.Equal("first", titles[0]);
        Assert.Equal("last", titles[^1]);
        Assert.Equal("n99", titles[1]);
        Assert.Equal(todo.Cards.Count, todo.Cards.Select(c => c.Position).Distinct().Count());
    }

    [Fact]
    public void Wip_limit_blocks_new_and_incoming_cards()
    {
        var board = NewBoard();
        var (todo, doing) = (board.Columns[0], board.Columns[1]);
        doing.SetWipLimit(1);
        board.AddCard(doing.Id, "X");
        var a = board.AddCard(todo.Id, "A");

        Assert.Throws<DomainException>(() => board.AddCard(doing.Id, "Y"));
        Assert.Throws<DomainException>(() => board.MoveCard(a.Id, doing.Id, 0));
        Assert.Equal(todo.Id, a.ColumnId);
    }

    [Fact]
    public void Wip_limit_allows_reordering_inside_full_column()
    {
        var board = NewBoard();
        var doing = board.Columns[1];
        var x = board.AddCard(doing.Id, "X");
        board.AddCard(doing.Id, "Y");
        doing.SetWipLimit(2);

        board.MoveCard(x.Id, doing.Id, 1);

        Assert.Equal(["Y", "X"], Titles(doing));
    }

    [Fact]
    public void Wip_limit_must_be_positive()
    {
        var board = NewBoard();
        Assert.Throws<DomainException>(() => board.Columns[0].SetWipLimit(0));
    }

    [Fact]
    public void RemoveColumn_with_cards_is_rejected()
    {
        var board = NewBoard();
        var todo = board.Columns[0];
        board.AddCard(todo.Id, "A");

        Assert.Throws<DomainException>(() => board.RemoveColumn(todo.Id));
    }

    [Fact]
    public void MoveColumn_reorders_columns()
    {
        var board = NewBoard();
        var done = board.Columns[2];

        board.MoveColumn(done.Id, 0);

        Assert.Equal(["Hecho", "Por hacer", "En curso"], board.Columns.Select(c => c.Name));
    }

    [Fact]
    public void Operations_on_foreign_ids_fail()
    {
        var board = NewBoard();
        Assert.Throws<EntityNotFoundException>(() => board.GetCard(Guid.NewGuid()));
        Assert.Throws<EntityNotFoundException>(() => board.AddCard(Guid.NewGuid(), "A"));
    }
}
