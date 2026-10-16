using System.ComponentModel;
using System.Text;
using Kanban.Api.Services;
using Kanban.Domain.Common;
using Kanban.Shared.Boards;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Kanban.Api.Mcp;

/// <summary>Prompts MCP: plantillas que el usuario lanza desde Claude (p. ej. /mcp__kanban__resumen_estado).</summary>
[McpServerPromptType]
public sealed class KanbanPrompts(BoardService boards)
{
    /// <summary>Etiquetas que marcan una tarjeta como bloqueada.</summary>
    public static readonly string[] BlockedLabels = ["bloqueado", "bloqueada", "blocked"];

    [McpServerPrompt(Name = "planificar_feature", Title = "Planificar una feature")]
    [Description("Divide una idea en tarjetas pequeñas y accionables y las crea en el tablero.")]
    public async Task<string> PlanFeature(
        [Description("La idea o feature a planificar.")] string idea,
        [Description("Id del tablero donde crear las tarjetas. Si se omite, Claude preguntará cuál usar.")] string? boardId = null,
        CancellationToken ct = default)
    {
        var target = "";
        if (Guid.TryParse(boardId, out var id))
        {
            var board = await Guarded(() => boards.GetAsync(id, ct));
            target = $"Tablero: «{board.Name}» (id {board.Id}). Columnas: {string.Join(", ", board.Columns.Select(c => c.Name))}.\n";
        }

        return $"""
            Quiero planificar esta feature en mi tablero Kanban:

            {idea}

            {target}Hazlo así:
            1. {(target.Length == 0 ? "Usa list_boards y pregúntame en qué tablero crear las tarjetas si no está claro." : "Usa get_board para ver qué tarjetas existen y no duplicar trabajo.")}
            2. Divide la feature en 3-8 tarjetas pequeñas e independientes (idealmente menos de un día cada una), en el orden en que conviene hacerlas.
            3. Antes de crear nada, enséñame la lista propuesta (título, prioridad, etiquetas) y espera a que la confirme.
            4. Cuando la confirme, crea cada tarjeta con create_card en la primera columna. En la descripción (Markdown) pon el objetivo y una lista de criterios de aceptación. Usa prioridad High solo para lo que bloquea al resto.
            5. Termina con un resumen de lo creado.
            """;
    }

    [McpServerPrompt(Name = "resumen_estado", Title = "Resumen del estado")]
    [Description("Resume el estado de un tablero: qué está bloqueado, qué ha vencido y qué se ha avanzado esta semana.")]
    public async Task<string> StatusSummary(
        [Description("Id del tablero.")] string boardId,
        CancellationToken ct = default)
    {
        if (!Guid.TryParse(boardId, out var id))
            throw new McpException("boardId no es un id válido. Usa list_boards para obtenerlo.");

        var board = await Guarded(() => boards.GetAsync(id, ct));
        var activity = await Guarded(() => boards.GetActivityAsync(id, 200, ct));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var done = board.Columns.Count > 0 ? board.Columns[^1] : null;

        var open = board.Columns.Where(c => c != done).SelectMany(c => c.Cards.Select(card => (Column: c, Card: card))).ToList();
        var overdue = open.Where(x => x.Card.DueDate is { } due && due < today).ToList();
        var blocked = open.Where(x => x.Card.Labels.Any(l => BlockedLabels.Contains(l, StringComparer.OrdinalIgnoreCase))).ToList();
        var fullColumns = board.Columns.Where(c => c.WipLimit is { } limit && c.Cards.Count >= limit).ToList();
        var recent = activity.Where(a => a.CreatedAt >= weekAgo).ToList();

        var data = new StringBuilder();
        data.AppendLine($"Tablero «{board.Name}» a fecha {today:yyyy-MM-dd}.");
        data.AppendLine("Columnas: " + string.Join(" | ", board.Columns.Select(c => $"{c.Name}: {c.Cards.Count}{(c.WipLimit is { } l ? $"/{l}" : "")}")));
        AppendCards(data, "Vencidas", overdue, x => $"vencía {x.Card.DueDate:yyyy-MM-dd}");
        AppendCards(data, "Bloqueadas (etiqueta bloqueado/blocked)", blocked, _ => null);
        data.AppendLine(fullColumns.Count == 0
            ? "Columnas en su límite WIP: ninguna."
            : "Columnas en su límite WIP: " + string.Join(", ", fullColumns.Select(c => c.Name)) + ".");
        data.AppendLine($"Actividad de los últimos 7 días ({recent.Count} cambios):");
        foreach (var a in recent.Take(40))
            data.AppendLine($"- {a.CreatedAt:yyyy-MM-dd} {(a.Source == ActivitySourceDto.Ai ? "Claude" : a.UserName)} {a.Summary}");

        return $"""
            Hazme un resumen breve del estado de mi tablero Kanban con estos datos (ya calculados, no hace falta consultar nada más):

            {data}
            Estructura el resumen en tres apartados: «Bloqueado», «Vencido» y «Avances de la semana».
            Si un apartado está vacío, dilo en una línea. Termina con como mucho tres próximos pasos concretos.
            """;
    }

    private static void AppendCards(StringBuilder data, string title, List<(ColumnDto Column, CardDto Card)> cards, Func<(ColumnDto Column, CardDto Card), string?> extra)
    {
        if (cards.Count == 0)
        {
            data.AppendLine($"{title}: ninguna.");
            return;
        }
        data.AppendLine($"{title}:");
        foreach (var x in cards)
        {
            var detail = extra(x);
            data.AppendLine($"- «{x.Card.Title}» en {x.Column.Name}{(detail is null ? "" : $", {detail}")} (id {x.Card.Id})");
        }
    }

    private static async Task<T> Guarded<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainException e)
        {
            throw new McpException(e.Message);
        }
    }
}
