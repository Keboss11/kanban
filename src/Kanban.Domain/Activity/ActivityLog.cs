namespace Kanban.Domain.Activity;

/// <summary>Origen de un cambio: la interfaz web o un agente de IA vía MCP.</summary>
public enum ActivitySource
{
    Web = 0,
    Ai = 1,
}

public enum ActivityAction
{
    BoardCreated,
    BoardUpdated,
    ColumnCreated,
    ColumnUpdated,
    ColumnMoved,
    ColumnDeleted,
    CardCreated,
    CardUpdated,
    CardMoved,
    CardDeleted,
    CommentAdded,
}

/// <summary>
/// Quién hizo qué y cuándo. <see cref="Summary"/> es texto ya legible,
/// p. ej. "movió «Login» a Hecho"; la UI antepone el autor (o 🤖 si es IA).
/// </summary>
public sealed class ActivityLog
{
    private ActivityLog()
    {
        UserId = null!;
        Summary = null!;
    }

    public ActivityLog(Guid boardId, string userId, ActivitySource source, ActivityAction action, string summary, Guid? cardId = null)
    {
        BoardId = boardId;
        UserId = userId;
        Source = source;
        Action = action;
        Summary = summary;
        CardId = cardId;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid BoardId { get; private set; }
    public Guid? CardId { get; private set; }
    public string UserId { get; private set; }
    public ActivitySource Source { get; private set; }
    public ActivityAction Action { get; private set; }
    public string Summary { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
