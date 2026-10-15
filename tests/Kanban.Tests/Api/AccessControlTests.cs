using System.Net;
using Kanban.Shared.Boards;

namespace Kanban.Tests.Api;

/// <summary>Un usuario no puede ver ni tocar nada de un tablero ajeno: siempre 404.</summary>
public sealed class AccessControlTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    [Fact]
    public async Task Other_user_gets_404_on_every_board_operation()
    {
        var owner = await factory.CreateUserClientAsync();
        var intruder = await factory.CreateUserClientAsync();
        var board = await owner.CreateBoardAsync("Privado");
        var column = board.Columns[0].Id;
        var card = await owner.CreateCardAsync(board.Id, column, "Secreto");
        var b = $"/api/boards/{board.Id}";
        var c = $"{b}/cards/{card.Id}";

        var attempts = new (string Name, Func<Task<HttpResponseMessage>> Call)[]
        {
            ("get board", () => intruder.GetAsync(b)),
            ("update board", () => intruder.PutJsonAsync(b, new UpdateBoardRequest("Mío"))),
            ("delete board", () => intruder.DeleteAsync(b)),
            ("activity", () => intruder.GetAsync($"{b}/activity")),
            ("add column", () => intruder.PostJsonAsync($"{b}/columns", new CreateColumnRequest("X"))),
            ("delete column", () => intruder.DeleteAsync($"{b}/columns/{column}")),
            ("create card", () => intruder.PostJsonAsync($"{b}/cards", new CreateCardRequest(column, "X"))),
            ("get card", () => intruder.GetAsync(c)),
            ("update card", () => intruder.PutJsonAsync(c, new UpdateCardRequest("X"))),
            ("move card", () => intruder.PostJsonAsync($"{c}/move", new MoveCardRequest(column, 0))),
            ("delete card", () => intruder.DeleteAsync(c)),
            ("comments", () => intruder.GetAsync($"{c}/comments")),
            ("comment", () => intruder.PostJsonAsync($"{c}/comments", new CreateCommentRequest("X"))),
        };

        foreach (var (name, call) in attempts)
        {
            var response = await call();
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{name}: {(int)response.StatusCode}");
        }

        // Nada ha cambiado para el propietario.
        var after = await owner.GetBoardAsync(board.Id);
        Assert.Equal("Privado", after.Name);
        Assert.Equal("Secreto", Assert.Single(after.Columns[0].Cards).Title);
    }

    [Fact]
    public async Task List_and_search_only_return_own_data()
    {
        var owner = await factory.CreateUserClientAsync();
        var other = await factory.CreateUserClientAsync();
        var board = await owner.CreateBoardAsync();
        await owner.CreateCardAsync(board.Id, board.Columns[0].Id, "Solo mío");

        Assert.Empty(await (await other.GetAsync("/api/boards")).ReadAsync<List<BoardSummaryDto>>());
        Assert.Empty(await (await other.GetAsync("/api/cards/search?text=mío")).ReadAsync<List<CardSearchResultDto>>());
        Assert.Empty(await (await other.GetAsync($"/api/cards/search?boardId={board.Id}")).ReadAsync<List<CardSearchResultDto>>());
    }
}
