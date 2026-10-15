namespace Kanban.Shared.Boards;

public sealed record CreateBoardRequest(string Name, string? Description = null, bool WithDefaultColumns = true);

public sealed record UpdateBoardRequest(string Name, string? Description = null);

public sealed record CreateColumnRequest(string Name, int? WipLimit = null);

public sealed record UpdateColumnRequest(string Name, int? WipLimit = null);

/// <summary>Índice destino (0 = primera). Se recorta al rango válido.</summary>
public sealed record MoveColumnRequest(int Index);

public sealed record CreateCardRequest(
    Guid ColumnId,
    string Title,
    string? Description = null,
    CardPriority Priority = CardPriority.Medium,
    IReadOnlyList<string>? Labels = null,
    DateOnly? DueDate = null);

public sealed record UpdateCardRequest(
    string Title,
    string? Description = null,
    CardPriority Priority = CardPriority.Medium,
    IReadOnlyList<string>? Labels = null,
    DateOnly? DueDate = null);

/// <summary>Columna destino e índice dentro de ella (en la lista final, 0 = arriba).</summary>
public sealed record MoveCardRequest(Guid ColumnId, int Index);

public sealed record CreateCommentRequest(string Text);

public sealed record CardSearchQuery(string? Text = null, string? Label = null, CardPriority? Priority = null, Guid? BoardId = null);
