using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Kanban.Shared.Auth;
using Kanban.Shared.Boards;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kanban.Tests.Api;

public sealed class McpTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    private static async Task<string> CreateTokenAsync(HttpClient user) =>
        (await (await user.PostJsonAsync("/api/tokens", new CreateTokenRequest("tests")))
            .ReadAsync<CreatedTokenDto>(HttpStatusCode.Created)).Value;

    private async Task<McpClient> ConnectAsync(string token)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(factory.Server.BaseAddress, "mcp") }, http, null, ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    /// <summary>Usuario con un tablero y un cliente MCP conectado con su token.</summary>
    private async Task<(HttpClient User, BoardDto Board, McpClient Mcp)> ArrangeAsync()
    {
        var user = await factory.CreateUserClientAsync();
        var board = await user.CreateBoardAsync("Proyecto IA");
        return (user, board, await ConnectAsync(await CreateTokenAsync(user)));
    }

    private static async Task<CallToolResult> CallAsync(McpClient mcp, string tool, Dictionary<string, object?> args) =>
        await mcp.CallToolAsync(tool, args);

    private static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private static T Json<T>(CallToolResult result)
    {
        Assert.True(result.IsError is not true, Text(result));
        return JsonSerializer.Deserialize<T>(Text(result), KanbanApiFactory.Json)!;
    }

    [Fact]
    public async Task Lists_all_tools()
    {
        var (_, _, mcp) = await ArrangeAsync();
        await using var _ = mcp;

        var tools = await mcp.ListToolsAsync();

        Assert.Equal(
            ["add_comment", "create_board", "create_card", "delete_card", "get_board", "list_boards", "move_card", "search_cards", "update_card"],
            tools.Select(t => t.Name).Order());
    }

    [Fact]
    public async Task Create_and_move_card_by_column_name_are_recorded_as_ai()
    {
        var (user, board, mcp) = await ArrangeAsync();
        await using var _ = mcp;

        var card = Json<CardDto>(await CallAsync(mcp, "create_card", new()
        {
            ["boardId"] = board.Id, ["column"] = "por hacer", ["title"] = "Escribir tests",
            ["priority"] = "High", ["labels"] = new[] { "qa" }, ["dueDate"] = "2026-12-01",
        }));
        Assert.Equal(CardPriority.High, card.Priority);
        Assert.Equal(new DateOnly(2026, 12, 1), card.DueDate);

        var moved = Json<CardDto>(await CallAsync(mcp, "move_card", new()
        {
            ["boardId"] = board.Id, ["cardId"] = card.Id, ["column"] = "Hecho",
        }));
        Assert.Equal(board.Columns[2].Id, moved.ColumnId);

        var activity = await (await user.GetAsync($"/api/boards/{board.Id}/activity")).ReadAsync<List<ActivityDto>>();
        Assert.Equal("movió «Escribir tests» a Hecho", activity[0].Summary);
        Assert.Equal(ActivitySourceDto.Ai, activity[0].Source);
        Assert.Equal(ActivitySourceDto.Web, activity[^1].Source); // el tablero lo creó el usuario desde la web
    }

    [Fact]
    public async Task Business_errors_reach_the_model_with_their_message()
    {
        var (user, board, mcp) = await ArrangeAsync();
        await using var _ = mcp;
        await user.PutJsonAsync($"/api/boards/{board.Id}/columns/{board.Columns[1].Id}", new UpdateColumnRequest("En curso", 1));
        await user.CreateCardAsync(board.Id, board.Columns[1].Id, "Ocupando");
        var card = await user.CreateCardAsync(board.Id, board.Columns[0].Id, "Espera");

        var wip = await CallAsync(mcp, "move_card", new() { ["boardId"] = board.Id, ["cardId"] = card.Id, ["column"] = "En curso" });
        Assert.True(wip.IsError);
        Assert.Contains("límite WIP", Text(wip));

        var badColumn = await CallAsync(mcp, "create_card", new() { ["boardId"] = board.Id, ["column"] = "Backlog", ["title"] = "X" });
        Assert.True(badColumn.IsError);
        Assert.Contains("Por hacer, En curso, Hecho", Text(badColumn));

        var badDate = await CallAsync(mcp, "create_card", new() { ["boardId"] = board.Id, ["column"] = "Hecho", ["title"] = "X", ["dueDate"] = "mañana" });
        Assert.True(badDate.IsError);
        Assert.Contains("YYYY-MM-DD", Text(badDate));
    }

    [Fact]
    public async Task Update_card_only_changes_given_fields()
    {
        var (user, board, mcp) = await ArrangeAsync();
        await using var _ = mcp;
        var card = await user.CreateCardAsync(board.Id, board.Columns[0].Id, "Original", CardPriority.Low, ["a"], "desc");

        var updated = Json<CardDto>(await CallAsync(mcp, "update_card", new()
        {
            ["boardId"] = board.Id, ["cardId"] = card.Id, ["priority"] = "Urgent",
        }));

        Assert.Equal("Original", updated.Title);
        Assert.Equal("desc", updated.Description);
        Assert.Equal(["a"], updated.Labels);
        Assert.Equal(CardPriority.Urgent, updated.Priority);
    }

    [Fact]
    public async Task Tools_cannot_touch_other_users_boards()
    {
        var owner = await factory.CreateUserClientAsync();
        var foreign = await owner.CreateBoardAsync("Ajeno");
        var (_, _, mcp) = await ArrangeAsync();
        await using var _ = mcp;

        var boards = Json<List<BoardSummaryDto>>(await CallAsync(mcp, "list_boards", []));
        Assert.DoesNotContain(boards, b => b.Id == foreign.Id);

        var result = await CallAsync(mcp, "get_board", new() { ["boardId"] = foreign.Id });
        Assert.True(result.IsError);
        Assert.Contains("no existe", Text(result));
    }

    [Fact]
    public async Task Create_board_comment_search_and_delete()
    {
        var (_, _, mcp) = await ArrangeAsync();
        await using var _ = mcp;

        var board = Json<BoardDto>(await CallAsync(mcp, "create_board", new() { ["name"] = "Creado por Claude" }));
        Assert.Equal(3, board.Columns.Count);
        var card = Json<CardDto>(await CallAsync(mcp, "create_card", new() { ["boardId"] = board.Id, ["column"] = "Por hacer", ["title"] = "Investigar OAuth" }));

        Json<CommentDto>(await CallAsync(mcp, "add_comment", new() { ["boardId"] = board.Id, ["cardId"] = card.Id, ["text"] = "Empiezo por la spec." }));
        var found = Json<List<CardSearchResultDto>>(await CallAsync(mcp, "search_cards", new() { ["text"] = "oauth" }));
        Assert.Equal("Creado por Claude", Assert.Single(found).BoardName);

        var deleted = await CallAsync(mcp, "delete_card", new() { ["boardId"] = board.Id, ["cardId"] = card.Id });
        Assert.True(deleted.IsError is not true, Text(deleted));
        Assert.Empty(Json<List<CardSearchResultDto>>(await CallAsync(mcp, "search_cards", new() { ["text"] = "oauth" })));
    }

    [Fact]
    public async Task Revoked_or_invalid_token_is_rejected()
    {
        var user = await factory.CreateUserClientAsync();
        var token = await CreateTokenAsync(user);
        var id = (await (await user.GetAsync("/api/tokens")).ReadAsync<List<PersonalAccessTokenDto>>())[0].Id;
        await user.DeleteAsync($"/api/tokens/{id}");

        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(token));
        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync("kbn_inventado"));
    }

    // ---- Prompts ----

    private static string PromptText(GetPromptResult result) =>
        string.Concat(result.Messages.Select(m => m.Content).OfType<TextContentBlock>().Select(c => c.Text));

    [Fact]
    public async Task Lists_prompts()
    {
        var (_, _, mcp) = await ArrangeAsync();
        await using var _ = mcp;

        var prompts = await mcp.ListPromptsAsync();

        Assert.Equal(["planificar_feature", "resumen_estado"], prompts.Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Status_summary_includes_overdue_blocked_and_recent_progress()
    {
        var (user, board, mcp) = await ArrangeAsync();
        await using var _ = mcp;
        var (todo, done) = (board.Columns[0].Id, board.Columns[2].Id);
        await user.PostJsonAsync($"/api/boards/{board.Id}/cards",
            new CreateCardRequest(todo, "Factura atrasada", DueDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3)));
        await user.CreateCardAsync(board.Id, todo, "Esperando API externa", labels: ["Bloqueado"]);
        var finished = await user.CreateCardAsync(board.Id, todo, "Login");
        await user.PostJsonAsync($"/api/boards/{board.Id}/cards/{finished.Id}/move", new MoveCardRequest(done, 0));

        var text = PromptText(await mcp.GetPromptAsync("resumen_estado", new Dictionary<string, object?> { ["boardId"] = board.Id.ToString() }));

        Assert.Contains("«Factura atrasada» en Por hacer, vencía", text);
        Assert.Contains("«Esperando API externa» en Por hacer", text);
        Assert.Contains("movió «Login» a Hecho", text);
        Assert.Contains("Bloqueado", text);
    }

    [Fact]
    public async Task Plan_feature_mentions_idea_and_board()
    {
        var (_, board, mcp) = await ArrangeAsync();
        await using var _ = mcp;

        var text = PromptText(await mcp.GetPromptAsync("planificar_feature", new Dictionary<string, object?>
        {
            ["idea"] = "Exportar el tablero a CSV",
            ["boardId"] = board.Id.ToString(),
        }));

        Assert.Contains("Exportar el tablero a CSV", text);
        Assert.Contains("«Proyecto IA»", text);
        Assert.Contains("create_card", text);
    }
}
