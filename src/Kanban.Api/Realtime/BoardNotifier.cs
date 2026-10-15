using Kanban.Shared.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Kanban.Api.Realtime;

/// <summary>Avisa a los clientes conectados a un tablero de que algo ha cambiado.</summary>
public interface IBoardNotifier
{
    Task BoardChangedAsync(BoardChangedEvent change);
    Task BoardDeletedAsync(Guid boardId);
}

public sealed class SignalRBoardNotifier(IHubContext<BoardHub> hub, IHttpContextAccessor http) : IBoardNotifier
{
    public Task BoardChangedAsync(BoardChangedEvent change) =>
        Recipients(change.BoardId).SendAsync(BoardHubContract.BoardChanged, change);

    public Task BoardDeletedAsync(Guid boardId) =>
        Recipients(boardId).SendAsync(BoardHubContract.BoardDeleted, boardId);

    /// <summary>Todo el grupo salvo la conexión que originó la petición (ya tiene el cambio aplicado).</summary>
    private IClientProxy Recipients(Guid boardId)
    {
        var group = BoardHub.Group(boardId);
        var origin = http.HttpContext?.Request.Headers[BoardHubContract.ConnectionIdHeader].ToString();
        return string.IsNullOrEmpty(origin)
            ? hub.Clients.Group(group)
            : hub.Clients.GroupExcept(group, [origin]);
    }
}
