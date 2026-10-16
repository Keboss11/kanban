using Kanban.Infrastructure.Identity;
using Kanban.Shared.Auth;
using Microsoft.AspNetCore.Identity;

namespace Kanban.Api.Auth;

public static class AuthEndpoints
{
    /// <summary>
    /// /auth/register, /auth/login, /auth/refresh… de MapIdentityApi (tokens bearer, sin cookies:
    /// la web y la API viven en dominios distintos) más /auth/me.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Auth").ProtectDemoAccount();
        auth.MapIdentityApi<ApplicationUser>();

        auth.MapGet("/me", async (ICurrentUser current, UserManager<ApplicationUser> users) =>
        {
            var user = await users.FindByIdAsync(current.Id);
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(new UserInfoDto(user.Id, user.Email!, user.DisplayName ?? user.Email!));
        }).RequireAuthorization();

        return app;
    }
}
