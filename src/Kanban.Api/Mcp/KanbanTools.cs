using System.ComponentModel;
using System.Globalization;
using Kanban.Api.Services;
using Kanban.Domain.Common;
using Kanban.Shared.Boards;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Kanban.Api.Mcp;

/// <summary>
/// Herramientas MCP. Solo traducen parámetros: toda la lógica, la validación, el control de acceso,
/// el historial y el aviso en tiempo real los ponen los mismos servicios que usa la web.
/// </summary>
[McpServerToolType]
public sealed class KanbanTools(BoardService boards, CardService cards)
{
    [McpServerTool(Name = "list_boards", Title = "Listar tableros", ReadOnly = true, Idempotent = true)]
    [Description("Lista los tableros del usuario con su número de tarjetas.")]
    public Task<IReadOnlyList<BoardSummaryDto>> ListBoards(CancellationToken ct) =>
        Run(() => boards.ListAsync(ct));

    [McpServerTool(Name = "get_board", Title = "Ver tablero", ReadOnly = true, Idempotent = true)]
    [Description("Devuelve un tablero con sus columnas (en orden) y las tarjetas de cada columna (de arriba abajo).")]
    public Task<BoardDto> GetBoard(
        [Description("Id del tablero (de list_boards).")] Guid boardId,
        CancellationToken ct) =>
        Run(() => boards.GetAsync(boardId, ct));

    [McpServerTool(Name = "create_board", Title = "Crear tablero", Destructive = false)]
    [Description("Crea un tablero con las columnas por defecto: Por hacer, En curso y Hecho.")]
    public Task<BoardDto> CreateBoard(
        [Description("Nombre del tablero.")] string name,
        [Description("Descripción opcional.")] string? description = null,
        CancellationToken ct = default) =>
        Run(() => boards.CreateAsync(new CreateBoardRequest(name, description), ct));

    [McpServerTool(Name = "create_card", Title = "Crear tarjeta", Destructive = false)]
    [Description("Crea una tarjeta al final de una columna. Respeta el límite WIP de la columna.")]
    public Task<CardDto> CreateCard(
        [Description("Id del tablero.")] Guid boardId,
        [Description("Columna destino: su nombre (p. ej. \"Por hacer\") o su id.")] string column,
        [Description("Título corto de la tarjeta.")] string title,
        [Description("Descripción en Markdown.")] string? description = null,
        [Description("Prioridad: Low, Medium, High o Urgent. Por defecto Medium.")] CardPriority priority = CardPriority.Medium,
        [Description("Etiquetas (máximo 10).")] string[]? labels = null,
        [Description("Fecha límite en formato YYYY-MM-DD.")] string? dueDate = null,
        CancellationToken ct = default) =>
        Run(async () =>
        {
            var columnId = await ResolveColumnAsync(boardId, column, ct);
            return await cards.CreateAsync(boardId,
                new CreateCardRequest(columnId, title, description, priority, labels, ParseDate(dueDate)), ct);
        });

    [McpServerTool(Name = "update_card", Title = "Editar tarjeta", Destructive = false, Idempotent = true)]
    [Description("Modifica una tarjeta. Solo cambian los campos que se indiquen; el resto se conserva.")]
    public Task<CardDto> UpdateCard(
        [Description("Id del tablero.")] Guid boardId,
        [Description("Id de la tarjeta.")] Guid cardId,
        [Description("Nuevo título.")] string? title = null,
        [Description("Nueva descripción en Markdown. Cadena vacía para borrarla.")] string? description = null,
        [Description("Nueva prioridad: Low, Medium, High o Urgent.")] CardPriority? priority = null,
        [Description("Nueva lista completa de etiquetas (sustituye a la anterior). Lista vacía para quitarlas.")] string[]? labels = null,
        [Description("Nueva fecha límite YYYY-MM-DD. Cadena vacía para quitarla.")] string? dueDate = null,
        CancellationToken ct = default) =>
        Run(async () =>
        {
            var current = await cards.GetAsync(boardId, cardId, ct);
            var request = new UpdateCardRequest(
                title ?? current.Title,
                description ?? current.Description,
                priority ?? current.Priority,
                labels ?? current.Labels,
                dueDate is null ? current.DueDate : ParseDate(dueDate));
            return await cards.UpdateAsync(boardId, cardId, request, ct);
        });

    [McpServerTool(Name = "move_card", Title = "Mover tarjeta", Destructive = false, Idempotent = true)]
    [Description("Mueve una tarjeta a otra columna o a otra posición. Respeta el límite WIP de la columna destino.")]
    public Task<CardDto> MoveCard(
        [Description("Id del tablero.")] Guid boardId,
        [Description("Id de la tarjeta.")] Guid cardId,
        [Description("Columna destino: su nombre o su id.")] string column,
        [Description("Posición en la columna destino (0 = arriba). Si se omite, va al final.")] int? position = null,
        CancellationToken ct = default) =>
        Run(async () =>
        {
            var columnId = await ResolveColumnAsync(boardId, column, ct);
            return await cards.MoveAsync(boardId, cardId, new MoveCardRequest(columnId, position ?? int.MaxValue), ct);
        });

    [McpServerTool(Name = "delete_card", Title = "Borrar tarjeta", Destructive = true)]
    [Description("Borra una tarjeta y sus comentarios. No se puede deshacer.")]
    public Task<string> DeleteCard(
        [Description("Id del tablero.")] Guid boardId,
        [Description("Id de la tarjeta.")] Guid cardId,
        CancellationToken ct = default) =>
        Run(async () =>
        {
            await cards.DeleteAsync(boardId, cardId, ct);
            return "Tarjeta borrada.";
        });

    [McpServerTool(Name = "add_comment", Title = "Comentar tarjeta", Destructive = false)]
    [Description("Añade un comentario (Markdown) a una tarjeta.")]
    public Task<CommentDto> AddComment(
        [Description("Id del tablero.")] Guid boardId,
        [Description("Id de la tarjeta.")] Guid cardId,
        [Description("Texto del comentario.")] string text,
        CancellationToken ct = default) =>
        Run(() => cards.AddCommentAsync(boardId, cardId, new CreateCommentRequest(text), ct));

    [McpServerTool(Name = "search_cards", Title = "Buscar tarjetas", ReadOnly = true, Idempotent = true)]
    [Description($"Busca tarjetas en los tableros del usuario por texto (título y descripción), etiqueta o prioridad. Devuelve como mucho 50.")]
    public Task<IReadOnlyList<CardSearchResultDto>> SearchCards(
        [Description("Texto a buscar en título y descripción.")] string? text = null,
        [Description("Etiqueta exacta (sin distinguir mayúsculas).")] string? label = null,
        [Description("Prioridad: Low, Medium, High o Urgent.")] CardPriority? priority = null,
        [Description("Limitar a un tablero.")] Guid? boardId = null,
        CancellationToken ct = default) =>
        Run(() => cards.SearchAsync(new CardSearchQuery(text, label, priority, boardId), ct));

    // ---- Utilidades ----

    /// <summary>Acepta el nombre de la columna (sin distinguir mayúsculas) o su id.</summary>
    private async Task<Guid> ResolveColumnAsync(Guid boardId, string column, CancellationToken ct)
    {
        var board = await boards.GetAsync(boardId, ct);
        var match = board.Columns.FirstOrDefault(c => c.Id.ToString().Equals(column.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? board.Columns.FirstOrDefault(c => c.Name.Equals(column.Trim(), StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? throw new McpException(
            $"No hay ninguna columna «{column}». Columnas del tablero: {string.Join(", ", board.Columns.Select(c => c.Name))}.");
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new McpException($"Fecha «{value}» no válida: usa el formato YYYY-MM-DD.");
    }

    /// <summary>Los errores de negocio llegan a Claude con su mensaje para que pueda corregirse.</summary>
    private static async Task<T> Run<T>(Func<Task<T>> action)
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
