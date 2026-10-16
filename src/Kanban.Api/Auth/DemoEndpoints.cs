using System.Security.Cryptography;
using System.Text;
using Kanban.Infrastructure.Persistence.Seeding;

namespace Kanban.Api.Auth;

/// <summary>Cuenta demo pública: reinicio nocturno y protección frente a cambios de credenciales.</summary>
public static class DemoEndpoints
{
    public const string ResetKeyHeader = "X-Demo-Reset-Key";

    /// <summary>
    /// POST /admin/demo/reset con la cabecera X-Demo-Reset-Key = Demo:ResetKey.
    /// Lo llama un workflow programado de GitHub Actions (la API duerme en Render y un temporizador interno no se dispararía).
    /// Sin Demo:ResetKey configurada el endpoint no existe (404).
    /// </summary>
    public static IEndpointRouteBuilder MapDemoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/demo/reset", async (HttpRequest request, IConfiguration config, DemoDataSeeder seeder, CancellationToken ct) =>
        {
            var expected = config["Demo:ResetKey"];
            var password = config["Seed:DemoPassword"];
            if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(password))
                return Results.NotFound();

            var given = request.Headers[ResetKeyHeader].ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected)))
                return Results.Unauthorized();

            await seeder.ResetAsync(password, ct);
            return Results.NoContent();
        }).ExcludeFromDescription();

        return app;
    }

    /// <summary>Impide que alguien cambie la contraseña, el email o el 2FA de la cuenta demo (/auth/manage/*).</summary>
    public static RouteGroupBuilder ProtectDemoAccount(this RouteGroupBuilder auth)
    {
        auth.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (http.Request.Path.StartsWithSegments("/auth/manage")
                && HttpMethods.IsPost(http.Request.Method)
                && string.Equals(http.User.Identity?.Name, DemoDataSeeder.DemoEmail, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem("La cuenta demo no se puede modificar. Crea tu propia cuenta para eso.", statusCode: StatusCodes.Status403Forbidden);
            }
            return await next(context);
        });
        return auth;
    }
}
