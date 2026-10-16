using Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanban.Migrations.Postgres;

/// <summary>
/// Para generar migraciones de PostgreSQL con <c>dotnet ef</c>. No necesita conexión real para
/// <c>migrations add</c>; para <c>database update</c> usa la variable KANBAN_POSTGRES.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KanbanDbContext>
{
    public KanbanDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("KANBAN_POSTGRES")
            ?? "Host=localhost;Database=kanban;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<KanbanDbContext>();
        DatabaseProvider.Configure(options, DatabaseProvider.Postgres, connectionString);
        return new KanbanDbContext(options.Options);
    }
}
