using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kanban.Shared.Boards;

namespace Kanban.Tests.Api;

internal static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Esperado {(int)expected}, recibido {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, KanbanApiFactory.Json)!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, KanbanApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, KanbanApiFactory.Json);

    public static async Task<BoardDto> CreateBoardAsync(this HttpClient client, string name = "Proyecto") =>
        await (await client.PostJsonAsync("/api/boards", new CreateBoardRequest(name))).ReadAsync<BoardDto>(HttpStatusCode.Created);

    public static async Task<BoardDto> GetBoardAsync(this HttpClient client, Guid boardId) =>
        await (await client.GetAsync($"/api/boards/{boardId}")).ReadAsync<BoardDto>();

    public static async Task<CardDto> CreateCardAsync(this HttpClient client, Guid boardId, Guid columnId, string title,
        CardPriority priority = CardPriority.Medium, IReadOnlyList<string>? labels = null, string? description = null) =>
        await (await client.PostJsonAsync($"/api/boards/{boardId}/cards",
                new CreateCardRequest(columnId, title, description, priority, labels)))
            .ReadAsync<CardDto>(HttpStatusCode.Created);
}
