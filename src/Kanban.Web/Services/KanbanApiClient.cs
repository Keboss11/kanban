using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kanban.Shared.Boards;
using Kanban.Shared.Realtime;

namespace Kanban.Web.Services;

/// <summary>Cliente tipado de la API REST. Los errores llegan como <see cref="ApiException"/> con mensaje legible.</summary>
public sealed class KanbanApiClient(IHttpClientFactory httpFactory, BoardRealtime realtime)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private HttpClient Http => httpFactory.CreateClient(HttpClients.Api);

    // ---- Tableros ----
    public Task<List<BoardSummaryDto>> GetBoardsAsync() => GetAsync<List<BoardSummaryDto>>("api/boards");
    public Task<BoardDto> GetBoardAsync(Guid id) => GetAsync<BoardDto>($"api/boards/{id}");
    public Task<BoardDto> CreateBoardAsync(CreateBoardRequest req) => SendAsync<BoardDto>(HttpMethod.Post, "api/boards", req);
    public Task<BoardDto> UpdateBoardAsync(Guid id, UpdateBoardRequest req) => SendAsync<BoardDto>(HttpMethod.Put, $"api/boards/{id}", req);
    public Task DeleteBoardAsync(Guid id) => SendAsync(HttpMethod.Delete, $"api/boards/{id}");
    public Task<List<ActivityDto>> GetActivityAsync(Guid id, int take = 30) => GetAsync<List<ActivityDto>>($"api/boards/{id}/activity?take={take}");

    // ---- Columnas ----
    public Task<ColumnDto> AddColumnAsync(Guid boardId, CreateColumnRequest req) =>
        SendAsync<ColumnDto>(HttpMethod.Post, $"api/boards/{boardId}/columns", req);
    public Task<ColumnDto> UpdateColumnAsync(Guid boardId, Guid columnId, UpdateColumnRequest req) =>
        SendAsync<ColumnDto>(HttpMethod.Put, $"api/boards/{boardId}/columns/{columnId}", req);
    public Task<BoardDto> MoveColumnAsync(Guid boardId, Guid columnId, int index) =>
        SendAsync<BoardDto>(HttpMethod.Post, $"api/boards/{boardId}/columns/{columnId}/move", new MoveColumnRequest(index));
    public Task DeleteColumnAsync(Guid boardId, Guid columnId) =>
        SendAsync(HttpMethod.Delete, $"api/boards/{boardId}/columns/{columnId}");

    // ---- Tarjetas ----
    public Task<CardDto> CreateCardAsync(Guid boardId, CreateCardRequest req) =>
        SendAsync<CardDto>(HttpMethod.Post, $"api/boards/{boardId}/cards", req);
    public Task<CardDto> UpdateCardAsync(Guid boardId, Guid cardId, UpdateCardRequest req) =>
        SendAsync<CardDto>(HttpMethod.Put, $"api/boards/{boardId}/cards/{cardId}", req);
    public Task<CardDto> MoveCardAsync(Guid boardId, Guid cardId, Guid columnId, int index) =>
        SendAsync<CardDto>(HttpMethod.Post, $"api/boards/{boardId}/cards/{cardId}/move", new MoveCardRequest(columnId, index));
    public Task DeleteCardAsync(Guid boardId, Guid cardId) =>
        SendAsync(HttpMethod.Delete, $"api/boards/{boardId}/cards/{cardId}");

    // ---- Comentarios ----
    public Task<List<CommentDto>> GetCommentsAsync(Guid boardId, Guid cardId) =>
        GetAsync<List<CommentDto>>($"api/boards/{boardId}/cards/{cardId}/comments");
    public Task<CommentDto> AddCommentAsync(Guid boardId, Guid cardId, string text) =>
        SendAsync<CommentDto>(HttpMethod.Post, $"api/boards/{boardId}/cards/{cardId}/comments", new CreateCommentRequest(text));

    // ---- Infraestructura ----
    private Task<T> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url, null);

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body)
    {
        using var response = await SendRawAsync(method, url, body);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task SendAsync(HttpMethod method, string url)
    {
        using var _ = await SendRawAsync(method, url, null);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        // Esta pestaña ya aplica el cambio; la API no se lo reenviará por SignalR.
        if (realtime.ConnectionId is { } connectionId)
            request.Headers.Add(BoardHubContract.ConnectionIdHeader, connectionId);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            throw new ApiException(0, "No se pudo conectar con el servidor.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await ApiError.ReadMessageAsync(response);
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new ApiException(status, message);
        }
        return response;
    }
}
