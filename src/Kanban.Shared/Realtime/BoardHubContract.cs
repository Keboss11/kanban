using Kanban.Shared.Boards;

namespace Kanban.Shared.Realtime;

/// <summary>Nombres compartidos entre el hub de SignalR y sus clientes.</summary>
public static class BoardHubContract
{
    public const string Path = "/hubs/board";

    // Métodos del servidor
    public const string JoinBoard = "JoinBoard";
    public const string LeaveBoard = "LeaveBoard";

    // Eventos al cliente
    public const string BoardChanged = "BoardChanged";
    public const string BoardDeleted = "BoardDeleted";

    /// <summary>
    /// Cabecera HTTP con el id de conexión SignalR de quien hace el cambio:
    /// esa pestaña ya tiene el cambio aplicado y no recibe el aviso.
    /// </summary>
    public const string ConnectionIdHeader = "X-SignalR-Connection";
}

/// <summary>Algo ha cambiado en el tablero; el cliente recarga y puede mostrar el resumen.</summary>
public sealed record BoardChangedEvent(Guid BoardId, string UserName, ActivitySourceDto Source, string Summary);
