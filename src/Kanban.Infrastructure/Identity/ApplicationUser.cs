using Microsoft.AspNetCore.Identity;

namespace Kanban.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public const int DisplayNameMaxLength = 100;

    public string? DisplayName { get; set; }
}
