namespace Kanban.Domain.Common;

/// <summary>Se lanza cuando una operación viola una regla de negocio.</summary>
public sealed class DomainException(string message) : Exception(message);
