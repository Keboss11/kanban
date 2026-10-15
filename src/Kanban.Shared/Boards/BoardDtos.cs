namespace Kanban.Shared.Boards;

public enum CardPriority
{
    Low,
    Medium,
    High,
    Urgent,
}

public sealed record BoardSummaryDto(Guid Id, string Name, string? Description, int CardCount, DateTime CreatedAt);

public sealed record BoardDto(Guid Id, string Name, string? Description, IReadOnlyList<ColumnDto> Columns);

public sealed record ColumnDto(Guid Id, string Name, int? WipLimit, IReadOnlyList<CardDto> Cards);

public sealed record CardDto(
    Guid Id,
    Guid ColumnId,
    string Title,
    string? Description,
    CardPriority Priority,
    IReadOnlyList<string> Labels,
    DateOnly? DueDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Resultado de búsqueda: la tarjeta con el contexto de dónde está.</summary>
public sealed record CardSearchResultDto(Guid BoardId, string BoardName, string ColumnName, CardDto Card);

public sealed record CommentDto(Guid Id, Guid CardId, string AuthorName, string Text, DateTime CreatedAt);

public enum ActivitySourceDto
{
    Web,
    Ai,
}

public sealed record ActivityDto(Guid Id, Guid? CardId, string UserName, ActivitySourceDto Source, string Action, string Summary, DateTime CreatedAt);
