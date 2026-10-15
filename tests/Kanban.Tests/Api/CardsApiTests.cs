using System.Net;
using Kanban.Shared.Boards;

namespace Kanban.Tests.Api;

public sealed class CardsApiTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    [Fact]
    public async Task Card_lifecycle_create_update_move_comment_delete()
    {
        var client = await factory.CreateUserClientAsync();
        var board = await client.CreateBoardAsync();
        var (todo, done) = (board.Columns[0].Id, board.Columns[2].Id);
        var url = $"/api/boards/{board.Id}/cards";

        var card = await client.CreateCardAsync(board.Id, todo, "Login", CardPriority.High, ["auth"]);
        Assert.Equal(todo, card.ColumnId);

        var updated = await (await client.PutJsonAsync($"{url}/{card.Id}",
                new UpdateCardRequest("Login con Google", "**md**", CardPriority.Urgent, ["auth", "web"], new DateOnly(2026, 12, 1))))
            .ReadAsync<CardDto>();
        Assert.Equal(CardPriority.Urgent, updated.Priority);
        Assert.Equal(["auth", "web"], updated.Labels);

        var moved = await (await client.PostJsonAsync($"{url}/{card.Id}/move", new MoveCardRequest(done, 0))).ReadAsync<CardDto>();
        Assert.Equal(done, moved.ColumnId);
        var boardAfter = await client.GetBoardAsync(board.Id);
        Assert.Empty(boardAfter.Columns[0].Cards);
        Assert.Equal("Login con Google", Assert.Single(boardAfter.Columns[2].Cards).Title);

        await (await client.PostJsonAsync($"{url}/{card.Id}/comments", new CreateCommentRequest("¡Listo!")))
            .ReadAsync<CommentDto>(HttpStatusCode.Created);
        var comments = await (await client.GetAsync($"{url}/{card.Id}/comments")).ReadAsync<List<CommentDto>>();
        Assert.Equal("¡Listo!", Assert.Single(comments).Text);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{url}/{card.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{url}/{card.Id}")).StatusCode);
    }

    [Fact]
    public async Task Reordering_within_column_keeps_requested_order()
    {
        var client = await factory.CreateUserClientAsync();
        var board = await client.CreateBoardAsync();
        var todo = board.Columns[0].Id;
        await client.CreateCardAsync(board.Id, todo, "A");
        await client.CreateCardAsync(board.Id, todo, "B");
        var c = await client.CreateCardAsync(board.Id, todo, "C");

        await client.PostJsonAsync($"/api/boards/{board.Id}/cards/{c.Id}/move", new MoveCardRequest(todo, 1));

        var titles = (await client.GetBoardAsync(board.Id)).Columns[0].Cards.Select(x => x.Title);
        Assert.Equal(["A", "C", "B"], titles);
    }

    [Fact]
    public async Task Wip_limit_violation_returns_400()
    {
        var client = await factory.CreateUserClientAsync();
        var board = await client.CreateBoardAsync();
        var (todo, doing) = (board.Columns[0].Id, board.Columns[1].Id);
        await client.PutJsonAsync($"/api/boards/{board.Id}/columns/{doing}", new UpdateColumnRequest("En curso", 1));
        await client.CreateCardAsync(board.Id, doing, "Ocupada");
        var card = await client.CreateCardAsync(board.Id, todo, "Espera");

        var response = await client.PostJsonAsync($"/api/boards/{board.Id}/cards/{card.Id}/move", new MoveCardRequest(doing, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("límite WIP", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_card_returns_400()
    {
        var client = await factory.CreateUserClientAsync();
        var board = await client.CreateBoardAsync();

        var response = await client.PostJsonAsync($"/api/boards/{board.Id}/cards", new CreateCardRequest(board.Columns[0].Id, ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_json_returns_400()
    {
        var client = await factory.CreateUserClientAsync();
        var board = await client.CreateBoardAsync();

        var response = await client.PostAsync($"/api/boards/{board.Id}/cards",
            new StringContent("{\"title\": ", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Card_cannot_be_moved_to_column_of_another_board()
    {
        var client = await factory.CreateUserClientAsync();
        var first = await client.CreateBoardAsync("Uno");
        var second = await client.CreateBoardAsync("Dos");
        var card = await client.CreateCardAsync(first.Id, first.Columns[0].Id, "A");

        var response = await client.PostJsonAsync($"/api/boards/{first.Id}/cards/{card.Id}/move", new MoveCardRequest(second.Columns[0].Id, 0));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_by_text_label_and_priority()
    {
        var client = await factory.CreateUserClientAsync();
        var board = await client.CreateBoardAsync("Búsqueda");
        var todo = board.Columns[0].Id;
        await client.CreateCardAsync(board.Id, todo, "Arreglar LOGIN", CardPriority.High, ["Bug"]);
        await client.CreateCardAsync(board.Id, todo, "Diseño", CardPriority.Low, ["ux"], description: "pantalla de login");
        await client.CreateCardAsync(board.Id, todo, "Otra cosa", CardPriority.High);

        async Task<string[]> Search(string query) =>
            (await (await client.GetAsync($"/api/cards/search?{query}")).ReadAsync<List<CardSearchResultDto>>())
            .Select(r => r.Card.Title).Order().ToArray();

        Assert.Equal(["Arreglar LOGIN", "Diseño"], await Search("text=login"));
        Assert.Equal(["Arreglar LOGIN"], await Search("label=bug"));
        Assert.Equal(["Arreglar LOGIN", "Otra cosa"], await Search("priority=High"));
        Assert.Equal(["Arreglar LOGIN"], await Search("text=login&priority=High"));
    }
}
