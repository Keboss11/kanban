using Kanban.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Tests.Infrastructure;

/// <summary>Base de datos SQLite en memoria; vive mientras la conexión esté abierta.</summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public SqliteTestDatabase()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public KanbanDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<KanbanDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
