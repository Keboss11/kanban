using Microsoft.AspNetCore.Identity;

namespace Kanban.Api.Auth;

/// <summary>Mensajes de Identity en español (los que puede ver un usuario al registrarse).</summary>
public sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Ha ocurrido un error.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), $"Ya existe una cuenta con «{userName}».");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), $"Ya existe una cuenta con «{email}».");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), $"«{email}» no es un email válido.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), $"«{userName}» no es un nombre de usuario válido.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "Contraseña incorrecta.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"La contraseña debe tener al menos {uniqueChars} caracteres distintos.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe incluir un símbolo (p. ej. !, @, #).");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "La contraseña debe incluir un número.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "La contraseña debe incluir una minúscula.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "La contraseña debe incluir una mayúscula.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
