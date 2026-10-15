using System.Text.Json.Serialization;
using Kanban.Shared.Boards;
using Kanban.Shared.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.Tests.Api;

public sealed class BoardHubTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Conexión por WebSockets: el token viaja en la query, como desde el navegador.</summary>
    private async Task<HubConnection> ConnectAsync(HttpClient user)
    {
        var token = user.DefaultRequestHeaders.Authorization?.Parameter;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, BoardHubContract.Path.TrimStart('/')), o =>
            {
                o.Transports = HttpTransportType.WebSockets;
                // La negociación da el ConnectionId que la web envía en X-SignalR-Connection.
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.AccessTokenProvider = () => Task.FromResult(token);
                // Como en el navegador (que no puede poner cabeceras al WebSocket): token en ?access_token=.
                o.WebSocketFactory = async (ctx, ct) =>
                {
                    var query = ctx.Uri.Query.TrimStart('?');
                    var uri = token is null ? ctx.Uri : new UriBuilder(ctx.Uri)
                    {
                        Query = (query.Length > 0 ? query + "&" : "") + $"access_token={Uri.EscapeDataString(token)}",
                    }.Uri;
                    return await factory.Server.CreateWebSocketClient().ConnectAsync(uri, ct);
                };
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private static TaskCompletionSource<T> Listen<T>(HubConnection connection, string method)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<T>(method, value => tcs.TrySetResult(value));
        return tcs;
    }

    [Fact]
    public async Task Owner_receives_change_made_from_another_client()
    {
        var user = await factory.CreateUserClientAsync();
        var board = await user.CreateBoardAsync();
        await using var tab = await ConnectAsync(user);
        await tab.InvokeAsync(BoardHubContract.JoinBoard, board.Id);
        var changed = Listen<BoardChangedEvent>(tab, BoardHubContract.BoardChanged);

        await user.CreateCardAsync(board.Id, board.Columns[0].Id, "Nueva");

        var change = await changed.Task.WaitAsync(Timeout);
        Assert.Equal(board.Id, change.BoardId);
        Assert.Equal(ActivitySourceDto.Web, change.Source);
        Assert.Equal("creó «Nueva» en Por hacer", change.Summary);
    }

    [Fact]
    public async Task Originating_connection_is_excluded()
    {
        var user = await factory.CreateUserClientAsync();
        var board = await user.CreateBoardAsync();
        await using var origin = await ConnectAsync(user);
        await using var other = await ConnectAsync(user);
        await origin.InvokeAsync(BoardHubContract.JoinBoard, board.Id);
        await other.InvokeAsync(BoardHubContract.JoinBoard, board.Id);
        var originGot = Listen<BoardChangedEvent>(origin, BoardHubContract.BoardChanged);
        var otherGot = Listen<BoardChangedEvent>(other, BoardHubContract.BoardChanged);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/boards/{board.Id}/cards")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new CreateCardRequest(board.Columns[0].Id, "Desde origin"), options: KanbanApiFactory.Json),
        };
        Assert.NotNull(origin.ConnectionId);
        request.Headers.Add(BoardHubContract.ConnectionIdHeader, origin.ConnectionId);
        (await user.SendAsync(request)).EnsureSuccessStatusCode();

        await otherGot.Task.WaitAsync(Timeout);
        await Task.Delay(300);
        Assert.False(originGot.Task.IsCompleted);
    }

    [Fact]
    public async Task Board_deletion_is_broadcast()
    {
        var user = await factory.CreateUserClientAsync();
        var board = await user.CreateBoardAsync();
        await using var tab = await ConnectAsync(user);
        await tab.InvokeAsync(BoardHubContract.JoinBoard, board.Id);
        var deleted = Listen<Guid>(tab, BoardHubContract.BoardDeleted);

        await user.DeleteAsync($"/api/boards/{board.Id}");

        Assert.Equal(board.Id, await deleted.Task.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Cannot_join_someone_elses_board()
    {
        var owner = await factory.CreateUserClientAsync();
        var intruder = await factory.CreateUserClientAsync();
        var board = await owner.CreateBoardAsync();
        await using var connection = await ConnectAsync(intruder);

        var error = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(BoardHubContract.JoinBoard, board.Id));
        Assert.Contains("no existe", error.Message);
    }

    [Fact]
    public async Task Connection_without_token_is_rejected()
    {
        var anonymous = factory.CreateClient();

        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(anonymous));
    }
}
