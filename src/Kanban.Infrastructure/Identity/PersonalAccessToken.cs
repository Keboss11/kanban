using System.Security.Cryptography;
using System.Text;

namespace Kanban.Infrastructure.Identity;

/// <summary>
/// Token personal para clientes MCP (Claude Code, Claude Desktop). Solo se guarda el hash:
/// el valor en claro se muestra una única vez al crearlo.
/// </summary>
public sealed class PersonalAccessToken
{
    public const string ValuePrefix = "kbn_";
    public const int NameMaxLength = 60;

    private PersonalAccessToken()
    {
        UserId = null!;
        Name = null!;
        TokenHash = null!;
        DisplayPrefix = null!;
    }

    private PersonalAccessToken(string userId, string name, string tokenHash, string displayPrefix)
    {
        UserId = userId;
        Name = name;
        TokenHash = tokenHash;
        DisplayPrefix = displayPrefix;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string UserId { get; private set; }
    public string Name { get; private set; }

    /// <summary>SHA-256 en hexadecimal. El token tiene 256 bits aleatorios, así que no hace falta un hash lento.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Primeros caracteres del token, para reconocerlo en la lista sin poder usarlo.</summary>
    public string DisplayPrefix { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }

    /// <summary>Crea un token nuevo y devuelve también su valor en claro (no se puede recuperar después).</summary>
    public static (PersonalAccessToken Token, string Value) Create(string userId, string name)
    {
        var value = ValuePrefix + Base64Url(RandomNumberGenerator.GetBytes(32));
        var token = new PersonalAccessToken(userId, name, Hash(value), value[..(ValuePrefix.Length + 6)]);
        return (token, value);
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Actualiza la última fecha de uso como mucho una vez por minuto, para no escribir en cada petición.</summary>
    public bool MarkUsed(DateTime now)
    {
        if (LastUsedAt is { } last && now - last < TimeSpan.FromMinutes(1))
            return false;
        LastUsedAt = now;
        return true;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
