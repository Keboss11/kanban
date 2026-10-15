using Kanban.Api.Auth;
using Kanban.Api.Realtime;
using Kanban.Domain.Activity;
using Kanban.Domain.Boards;
using Kanban.Domain.Common;
using Kanban.Infrastructure.Persistence;
using Kanban.Shared.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services;

/// <summary>
/// Punto único de acceso a tableros: solo devuelve los del usuario actual, registra la actividad
/// y, al guardar, avisa en tiempo real. Todo servicio que lee o modifica un tablero pasa por aquí,
/// así la web y el MCP comparten reglas, historial y notificaciones.
/// </summary>
public sealed class BoardAccess(KanbanDbContext db, ICurrentUser user, IBoardNotifier notifier, ILogger<BoardAccess> logger)
{
    private readonly List<ActivityLog> _pending = [];

    public KanbanDbContext Db => db;
    public ICurrentUser User => user;

    public IQueryable<Board> OwnedBoards => db.Boards.Where(b => b.OwnerId == user.Id);

    /// <summary>Carga el tablero completo (columnas y tarjetas) o lanza 404 si no existe o es ajeno.</summary>
    public async Task<Board> LoadAsync(Guid boardId, CancellationToken ct) =>
        await OwnedBoards.Include("_columns._cards").SingleOrDefaultAsync(b => b.Id == boardId, ct)
        ?? throw new EntityNotFoundException("El tablero no existe.");

    public void Record(Guid boardId, ActivityAction action, string summary, Guid? cardId = null)
    {
        var entry = new ActivityLog(boardId, user.Id, user.Source, action, summary, cardId);
        db.ActivityLogs.Add(entry);
        _pending.Add(entry);
    }

    /// <summary>Guarda y, si ha ido bien, notifica cada cambio registrado.</summary>
    public async Task SaveAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        if (_pending.Count == 0)
            return;

        var entries = _pending.ToList();
        _pending.Clear();
        var userName = await db.Users.Where(u => u.Id == user.Id).Select(u => u.DisplayName ?? u.Email!).SingleAsync(ct);

        foreach (var entry in entries)
            await NotifySafelyAsync(() => notifier.BoardChangedAsync(
                new BoardChangedEvent(entry.BoardId, userName, entry.Source.ToDto(), entry.Summary)));
    }

    public Task NotifyDeletedAsync(Guid boardId) => NotifySafelyAsync(() => notifier.BoardDeletedAsync(boardId));

    /// <summary>El cambio ya está guardado: un fallo al notificar no debe convertir la petición en error.</summary>
    private async Task NotifySafelyAsync(Func<Task> notify)
    {
        try
        {
            await notify();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "No se pudo enviar la notificación en tiempo real.");
        }
    }
}
