using Kanban.Api.Auth;
using Kanban.Domain.Activity;
using Kanban.Domain.Boards;
using Kanban.Domain.Common;
using Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Api.Services;

/// <summary>
/// Punto único de acceso a tableros: solo devuelve los del usuario actual y registra la actividad.
/// Todo servicio que lee o modifica un tablero pasa por aquí.
/// </summary>
public sealed class BoardAccess(KanbanDbContext db, ICurrentUser user)
{
    public KanbanDbContext Db => db;
    public ICurrentUser User => user;

    public IQueryable<Board> OwnedBoards => db.Boards.Where(b => b.OwnerId == user.Id);

    /// <summary>Carga el tablero completo (columnas y tarjetas) o lanza 404 si no existe o es ajeno.</summary>
    public async Task<Board> LoadAsync(Guid boardId, CancellationToken ct) =>
        await OwnedBoards.Include("_columns._cards").SingleOrDefaultAsync(b => b.Id == boardId, ct)
        ?? throw new EntityNotFoundException("El tablero no existe.");

    public void Record(Guid boardId, ActivityAction action, string summary, Guid? cardId = null) =>
        db.ActivityLogs.Add(new ActivityLog(boardId, user.Id, user.Source, action, summary, cardId));

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
