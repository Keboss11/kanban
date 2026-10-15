using Kanban.Infrastructure.Persistence;
using Kanban.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Kanban") ?? "Data Source=kanban.db";

        services.AddDbContext<KanbanDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<DemoDataSeeder>();

        return services;
    }

    /// <summary>Aplica migraciones pendientes y, si hay contraseña demo configurada, siembra los datos de ejemplo.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KanbanDbContext>();
        await db.Database.MigrateAsync();

        var demoPassword = configuration["Seed:DemoPassword"];
        if (!string.IsNullOrEmpty(demoPassword))
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(demoPassword);
    }
}
