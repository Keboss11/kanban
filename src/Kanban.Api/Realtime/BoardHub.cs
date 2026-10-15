using Kanban.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Realtime;

/// <summary>Un grupo por tablero. Solo el propietario puede unirse a él.</summary>
[Authorize]
public sealed class BoardHub(KanbanDbContext db) : Hub
{
    public static string Group(Guid boardId) => $"board:{boardId}";

    public async Task JoinBoard(Guid boardId)
    {
        var userId = Context.UserIdentifier;
        if (userId is null || !await db.Boards.AnyAsync(b => b.Id == boardId && b.OwnerId == userId))
            throw new HubException("El tablero no existe.");

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(boardId));
    }

    public Task LeaveBoard(Guid boardId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(boardId));
}
