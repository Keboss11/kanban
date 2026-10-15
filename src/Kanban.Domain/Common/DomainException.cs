namespace Kanban.Domain.Common;

/// <summary>Se lanza cuando una operación viola una regla de negocio.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>El elemento no existe (o no pertenece al tablero/usuario). La API lo traduce a 404.</summary>
public sealed class EntityNotFoundException(string message) : DomainException(message);
