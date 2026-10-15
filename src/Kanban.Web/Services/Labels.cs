using Kanban.Shared.Boards;

namespace Kanban.Web.Services;

/// <summary>Textos de la interfaz para valores de la API.</summary>
public static class Labels
{
    public static string Priority(CardPriority priority) => priority switch
    {
        CardPriority.Low => "Baja",
        CardPriority.Medium => "Media",
        CardPriority.High => "Alta",
        CardPriority.Urgent => "Urgente",
        _ => priority.ToString(),
    };
}
