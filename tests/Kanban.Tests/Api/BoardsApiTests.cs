using System.Net;
using System.Net.Http.Json;
using Kanban.Shared.Auth;
using Kanban.Shared.Boards;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Tests.Api;

public sealed class BoardsApiTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    [Fact]
    public async Task Requests_without_token_are_rejected()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/boards")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Me_returns_current_user()
    {
        var client = await factory.CreateUserClientAsync("me@example.com");

        var me = await (await client.GetAsync("/auth/me")).ReadAsync<UserInfoDto>();

        Assert.Equal("me@example.com", me.Email);
    }

    [Fact]
    public async Task Create_get_update_list_and_delete_board()
    {
        var client = await factory.CreateUserClientAsync();

        var created = await client.CreateBoardAsync("Mi tablero");
        Assert.Equal(["Por hacer", "En curso", "Hecho"], created.Columns.Select(c => c.Name));

        var updated = await (await client.PutJsonAsync($"/api/boards/{created.Id}", new UpdateBoardRequest("Renombrado", "desc")))
            .ReadAsync<BoardDto>();
        Assert.Equal("Renombrado", updated.Name);

        var list = await (await client.GetAsync("/api/boards")).ReadAsync<List<BoardSummaryDto>>();
        Assert.Equal("Renombrado", Assert.Single(list).Name);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/boards/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/boards/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Empty_board_name_returns_problem_details()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.PostJsonAsync("/api/boards", new CreateBoardRequest("  "));

        var problem = await response.ReadAsync<ProblemDetails>(HttpStatusCode.BadRequest);
        Assert.Contains("obligatorio", problem.Detail);
    }

    [Fact]
    public async Task Columns_can_be_added_moved_updated_and_deleted()
    {
        var client = await factory.CreateUserClientAsync();
        var board = await client.CreateBoardAsync();
        var url = $"/api/boards/{board.Id}/columns";

        var review = await (await client.PostJsonAsync(url, new CreateColumnRequest("Revisión", 2))).ReadAsync<ColumnDto>(HttpStatusCode.Created);
        var moved = await (await client.PostJsonAsync($"{url}/{review.Id}/move", new MoveColumnRequest(0))).ReadAsync<BoardDto>();
        Assert.Equal("Revisión", moved.Columns[0].Name);

        var renamed = await (await client.PutJsonAsync($"{url}/{review.Id}", new UpdateColumnRequest("QA", null))).ReadAsync<ColumnDto>();
        Assert.Equal("QA", renamed.Name);
        Assert.Null(renamed.WipLimit);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{url}/{review.Id}")).StatusCode);
        Assert.Equal(3, (await client.GetBoardAsync(board.Id)).Columns.Count);
    }

    [Fact]
    public async Task Activity_lists_changes_with_web_source()
    {
        var client = await factory.CreateUserClientAsync();
        var board = await client.CreateBoardAsync("Historial");
        await client.CreateCardAsync(board.Id, board.Columns[0].Id, "Tarea");

        var activity = await (await client.GetAsync($"/api/boards/{board.Id}/activity")).ReadAsync<List<ActivityDto>>();

        Assert.Equal(["creó «Tarea» en Por hacer", "creó el tablero «Historial»"], activity.Select(a => a.Summary));
        Assert.All(activity, a => Assert.Equal(ActivitySourceDto.Web, a.Source));
        Assert.All(activity, a => Assert.Equal(DateTimeKind.Utc, a.CreatedAt.Kind));
    }
}
