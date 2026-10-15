namespace Kanban.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException($"{field} es obligatorio.");
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} no puede superar {maxLength} caracteres.");
        return trimmed;
    }

    public static string? Optional(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} no puede superar {maxLength} caracteres.");
        return trimmed;
    }
}
