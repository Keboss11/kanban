using Kanban.Domain.Activity;
using Kanban.Domain.Boards;
using Kanban.Infrastructure.Identity;
using Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Tests.Infrastructure;

public sealed class PersistenceTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static Task<Board> LoadBoard(KanbanDbContext db, Guid id) =>
        db.Boards.Include("_columns._cards").SingleAsync(b => b.Id == id);

    private async Task<string> CreateUser()
    {
        await using var db = _database.CreateContext();
        var user = new ApplicationUser { UserName = "ana@example.com", Email = "ana@example.com" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Board_with_columns_and_cards_round_trips()
    {
        var userId = await CreateUser();
        var board = Board.CreateWithDefaultColumns(userId, "Proyecto");
        board.AddCard(board.Columns[0].Id, "Login", "**Markdown**", Priority.High, ["auth", "web"], new DateOnly(2026, 11, 1));

        await using (var db = _database.CreateContext())
        {
            db.Boards.Add(board);
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var loaded = await LoadBoard(db, board.Id);
            Assert.Equal(Board.DefaultColumns, loaded.Columns.Select(c => c.Name));

            var card = Assert.Single(loaded.Columns[0].Cards);
            Assert.Equal("Login", card.Title);
            Assert.Equal(Priority.High, card.Priority);
            Assert.Equal(["auth", "web"], card.Labels);
            Assert.Equal(new DateOnly(2026, 11, 1), card.DueDate);
        }
    }

    [Fact]
    public async Task Moving_a_card_persists_new_column()
    {
        var userId = await CreateUser();
        var board = Board.CreateWithDefaultColumns(userId, "Proyecto");
        var card = board.AddCard(board.Columns[0].Id, "Login");
        var doneId = board.Columns[2].Id;

        await using (var db = _database.CreateContext())
        {
            db.Boards.Add(board);
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var loaded = await LoadBoard(db, board.Id);
            loaded.MoveCard(card.Id, doneId, 0);
            db.ActivityLogs.Add(new ActivityLog(board.Id, userId, ActivitySource.Ai, ActivityAction.CardMoved, "movió «Login» a Hecho", card.Id));
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var loaded = await LoadBoard(db, board.Id);
            Assert.Empty(loaded.Columns[0].Cards);
            Assert.Equal("Login", Assert.Single(loaded.GetColumn(doneId).Cards).Title);

            var log = Assert.Single(db.ActivityLogs);
            Assert.Equal(ActivitySource.Ai, log.Source);
        }
    }

    [Fact]
    public async Task Removing_a_card_deletes_it_and_its_comments()
    {
        var userId = await CreateUser();
        var board = Board.CreateWithDefaultColumns(userId, "Proyecto");
        var card = board.AddCard(board.Columns[0].Id, "Login");

        await using (var db = _database.CreateContext())
        {
            db.Boards.Add(board);
            db.Comments.Add(new Comment(card.Id, userId, "Hola"));
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            var loaded = await LoadBoard(db, board.Id);
            loaded.RemoveCard(card.Id);
            await db.SaveChangesAsync();
        }

        await using (var db = _database.CreateContext())
        {
            Assert.Empty(db.Cards);
            Assert.Empty(db.Comments);
        }
    }
}
