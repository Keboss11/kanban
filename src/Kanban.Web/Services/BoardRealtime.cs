using System.Text.Json.Serialization;
using Kanban.Shared.Realtime;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.Web.Services;

/// <summary>
/// Conexión SignalR compartida por toda la app. Se une al grupo del tablero abierto
/// y reenvía sus eventos a quien esté suscrito.
/// </summary>
public sealed class BoardRealtime : IAsyncDisposable
{
    private readonly AuthService _auth;
    private readonly Uri _hubUrl;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private HubConnection? _connection;
    private Guid? _board;

    public BoardRealtime(AuthService auth, Uri apiBase)
    {
        _auth = auth;
        _hubUrl = new Uri(apiBase, BoardHubContract.Path.TrimStart('/'));
        _auth.Changed += OnAuthChanged;
    }

    public event Func<BoardChangedEvent, Task>? Changed;
    public event Func<Guid, Task>? Deleted;
    /// <summary>Tras reconectar pudieron perderse avisos: hay que recargar.</summary>
    public event Func<Task>? Resynced;
    public event Action? StateChanged;

    public bool IsLive => _connection?.State == HubConnectionState.Connected && _board is not null;

    /// <summary>Id de esta conexión, para que la API no nos devuelva nuestros propios cambios.</summary>
    public string? ConnectionId => _connection?.State == HubConnectionState.Connected ? _connection.ConnectionId : null;

    public async Task JoinAsync(Guid boardId)
    {
        await _lock.WaitAsync();
        try
        {
            var connection = await EnsureStartedAsync();
            if (_board is { } previous && previous != boardId)
                await connection.InvokeAsync(BoardHubContract.LeaveBoard, previous);
            await connection.InvokeAsync(BoardHubContract.JoinBoard, boardId);
            _board = boardId;
        }
        finally
        {
            _lock.Release();
            StateChanged?.Invoke();
        }
    }

    public async Task LeaveAsync(Guid boardId)
    {
        if (_board != boardId)
            return;
        _board = null;
        StateChanged?.Invoke();
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync(BoardHubContract.LeaveBoard, boardId);
    }

    private async Task<HubConnection> EnsureStartedAsync()
    {
        _connection ??= Build();
        if (_connection.State == HubConnectionState.Disconnected)
            await _connection.StartAsync();
        return _connection;
    }

    private HubConnection Build()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, o => o.AccessTokenProvider = _auth.GetAccessTokenAsync)
            .WithAutomaticReconnect()
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        connection.On<BoardChangedEvent>(BoardHubContract.BoardChanged, e => Changed?.Invoke(e) ?? Task.CompletedTask);
        connection.On<Guid>(BoardHubContract.BoardDeleted, id => Deleted?.Invoke(id) ?? Task.CompletedTask);

        connection.Reconnecting += _ =>
        {
            StateChanged?.Invoke();
            return Task.CompletedTask;
        };
        connection.Reconnected += async _ =>
        {
            // El servidor olvida los grupos de la conexión anterior.
            if (_board is { } board)
                await connection.InvokeAsync(BoardHubContract.JoinBoard, board);
            StateChanged?.Invoke();
            if (Resynced is not null)
                await Resynced.Invoke();
        };
        connection.Closed += _ =>
        {
            StateChanged?.Invoke();
            return Task.CompletedTask;
        };
        return connection;
    }

    private async void OnAuthChanged()
    {
        // Al cerrar sesión la conexión no debe seguir escuchando con el token anterior.
        try
        {
            if (await _auth.IsLoggedInAsync() || _connection is null)
                return;
            _board = null;
            await _connection.StopAsync();
        }
        catch (Exception)
        {
            // async void: una excepción aquí tumbaría la app; la conexión se recrea en el próximo JoinAsync.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _auth.Changed -= OnAuthChanged;
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
