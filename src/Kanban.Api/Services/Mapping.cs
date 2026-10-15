using Kanban.Domain.Activity;
using Kanban.Domain.Boards;
using Kanban.Shared.Boards;

namespace Kanban.Api.Services;

internal static class Mapping
{
    public static BoardDto ToDto(this Board board) =>
        new(board.Id, board.Name, board.Description, board.Columns.Select(ToDto).ToList());

    public static ColumnDto ToDto(this Column column) =>
        new(column.Id, column.Name, column.WipLimit, column.Cards.Select(ToDto).ToList());

    public static CardDto ToDto(this Card card) =>
        new(card.Id, card.ColumnId, card.Title, card.Description, card.Priority.ToDto(),
            card.Labels, card.DueDate, card.CreatedAt, card.UpdatedAt);

    public static CardPriority ToDto(this Priority priority) => priority switch
    {
        Priority.Low => CardPriority.Low,
        Priority.Medium => CardPriority.Medium,
        Priority.High => CardPriority.High,
        Priority.Urgent => CardPriority.Urgent,
        _ => throw new ArgumentOutOfRangeException(nameof(priority)),
    };

    public static Priority ToDomain(this CardPriority priority) => priority switch
    {
        CardPriority.Low => Priority.Low,
        CardPriority.Medium => Priority.Medium,
        CardPriority.High => Priority.High,
        CardPriority.Urgent => Priority.Urgent,
        _ => throw new Kanban.Domain.Common.DomainException("Prioridad no válida."),
    };

    public static ActivitySourceDto ToDto(this ActivitySource source) =>
        source == ActivitySource.Ai ? ActivitySourceDto.Ai : ActivitySourceDto.Web;
}
