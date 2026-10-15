using Kanban.Domain.Common;

namespace Kanban.Domain.Boards;

public sealed class Card
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 10_000;
    public const int MaxLabels = 10;
    public const int LabelMaxLength = 30;

    private Card()
    {
        Title = null!;
    }

    internal Card(string title, string? description, Priority priority, IEnumerable<string>? labels, DateOnly? dueDate)
    {
        CreatedAt = DateTime.UtcNow;
        Title = null!;
        Update(title, description, priority, labels, dueDate);
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid ColumnId { get; private set; }
    public string Title { get; private set; }

    /// <summary>Markdown.</summary>
    public string? Description { get; private set; }
    public double Position { get; internal set; }
    public Priority Priority { get; private set; }
    public List<string> Labels { get; private set; } = [];
    public DateOnly? DueDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string title, string? description, Priority priority, IEnumerable<string>? labels, DateOnly? dueDate)
    {
        if (!Enum.IsDefined(priority))
            throw new DomainException("Prioridad no válida.");

        Title = Guard.Required(title, TitleMaxLength, "El título");
        Description = Guard.Optional(description, DescriptionMaxLength, "La descripción");
        Priority = priority;
        Labels = NormalizeLabels(labels);
        DueDate = dueDate;
        Touch();
    }

    public bool IsOverdue(DateOnly today) => DueDate is { } due && due < today;

    internal void PlaceIn(Guid columnId, double position)
    {
        ColumnId = columnId;
        Position = position;
    }

    internal void Touch() => UpdatedAt = DateTime.UtcNow;

    private static List<string> NormalizeLabels(IEnumerable<string>? labels)
    {
        var result = (labels ?? [])
            .Select(l => l?.Trim() ?? "")
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (result.Count > MaxLabels)
            throw new DomainException($"Una tarjeta no puede tener más de {MaxLabels} etiquetas.");
        if (result.Any(l => l.Length > LabelMaxLength))
            throw new DomainException($"Cada etiqueta puede tener como máximo {LabelMaxLength} caracteres.");
        return result;
    }
}
