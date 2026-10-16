namespace Kanban.Shared.Auth;

public sealed record PersonalAccessTokenDto(Guid Id, string Name, string Prefix, DateTime CreatedAt, DateTime? LastUsedAt);

public sealed record CreateTokenRequest(string Name);

/// <summary>Respuesta al crear un token: <see cref="Value"/> solo se devuelve esta vez.</summary>
public sealed record CreatedTokenDto(PersonalAccessTokenDto Token, string Value);
