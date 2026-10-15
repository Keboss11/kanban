using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanban.Infrastructure.Persistence;

/// <summary>Permite ejecutar <c>dotnet ef</c> sin arrancar la API.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KanbanDbContext>
{
    public KanbanDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KanbanDbContext>()
            .UseSqlite("Data Source=kanban.db")
            .Options;
        return new KanbanDbContext(options);
    }
}
