using Kanban.Domain.Activity;
using Kanban.Domain.Boards;
using Kanban.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Infrastructure.Persistence.Seeding;

/// <summary>
/// Crea el usuario demo con un tablero de ejemplo. Es idempotente: si el usuario
/// ya tiene tableros no hace nada. <see cref="ResetAsync"/> borra y vuelve a sembrar.
/// </summary>
public sealed class DemoDataSeeder(KanbanDbContext db, UserManager<ApplicationUser> users)
{
    public const string DemoEmail = "demo@kanban.local";

    public async Task SeedAsync(string demoPassword, CancellationToken ct = default)
    {
        var user = await EnsureDemoUserAsync(demoPassword);
        if (await db.Boards.AnyAsync(b => b.OwnerId == user.Id, ct))
            return;

        var board = BuildProjectBoard(user.Id, out var comments);
        db.Boards.Add(board);
        db.Comments.AddRange(comments);
        db.ActivityLogs.AddRange(BuildActivity(board, user.Id));
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetAsync(string demoPassword, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(DemoEmail);
        if (user is not null)
        {
            // Los comentarios y el historial del usuario restringen su borrado; se eliminan antes.
            await db.Comments.Where(c => c.AuthorId == user.Id).ExecuteDeleteAsync(ct);
            await db.ActivityLogs.Where(a => a.UserId == user.Id).ExecuteDeleteAsync(ct);
            await db.Boards.Where(b => b.OwnerId == user.Id).ExecuteDeleteAsync(ct);
        }
        await SeedAsync(demoPassword, ct);
    }

    private async Task<ApplicationUser> EnsureDemoUserAsync(string password)
    {
        var user = await users.FindByEmailAsync(DemoEmail);
        if (user is not null)
            return user;

        user = new ApplicationUser
        {
            UserName = DemoEmail,
            Email = DemoEmail,
            EmailConfirmed = true,
            DisplayName = "Demo",
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "No se pudo crear el usuario demo: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static Board BuildProjectBoard(string userId, out List<Comment> comments)
    {
        var board = new Board(userId, "Kanban + MCP", "Hoja de ruta de este mismo proyecto, gestionada con el propio Kanban.");
        var todo = board.AddColumn("Por hacer");
        var doing = board.AddColumn("En curso", wipLimit: 3);
        var review = board.AddColumn("Revisión");
        var done = board.AddColumn("Hecho");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        board.AddCard(done.Id, "F0 · Base", "Repositorio, solución, CI y README.", Priority.Medium, ["infra"]);
        var f1 = board.AddCard(doing.Id, "F1 · Dominio + base de datos",
            "- Entidades y reglas\n- EF Core con SQLite\n- Migraciones y **seed**", Priority.High, ["backend"], today.AddDays(2));
        board.AddCard(todo.Id, "F2 · API REST + autenticación",
            "Endpoints, Identity con tokens bearer y control de acceso por tablero.", Priority.High, ["backend", "auth"], today.AddDays(7));
        board.AddCard(todo.Id, "F3 · Interfaz Blazor",
            "Drag & drop con SortableJS y menú *Mover a…* como alternativa.", Priority.Medium, ["frontend"]);
        board.AddCard(todo.Id, "F4 · Tiempo real con SignalR", null, Priority.Medium, ["backend", "frontend"]);
        board.AddCard(todo.Id, "F5 · Servidor MCP", "Herramientas para que Claude gestione el tablero.", Priority.Urgent, ["mcp", "ia"]);
        var overdue = board.AddCard(review.Id, "Elegir proveedor de despliegue",
            "Render vs Azure App Service F1.", Priority.Low, ["infra"], today.AddDays(-1));

        comments =
        [
            new Comment(f1.Id, userId, "Las posiciones usan decimales con huecos para no renumerar la columna."),
            new Comment(overdue.Id, userId, "Pendiente de comparar tiempos de arranque en frío."),
        ];
        return board;
    }

    private static IEnumerable<ActivityLog> BuildActivity(Board board, string userId)
    {
        yield return new ActivityLog(board.Id, userId, ActivitySource.Web, ActivityAction.BoardCreated, $"creó el tablero «{board.Name}»");
        var f0 = board.Columns.SelectMany(c => c.Cards).First(c => c.Title.StartsWith("F0"));
        yield return new ActivityLog(board.Id, userId, ActivitySource.Ai, ActivityAction.CardMoved, $"movió «{f0.Title}» a Hecho", f0.Id);
    }
}
