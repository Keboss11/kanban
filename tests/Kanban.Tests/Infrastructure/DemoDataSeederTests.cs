using Kanban.Infrastructure.Identity;
using Kanban.Infrastructure.Persistence;
using Kanban.Infrastructure.Persistence.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.Tests.Infrastructure;

public sealed class DemoDataSeederTests : IAsyncLifetime
{
    private const string Password = "Demo1234!";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<KanbanDbContext>(o => o.UseSqlite(_connection));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<KanbanDbContext>();
        services.AddScoped<DemoDataSeeder>();
        _services = services.BuildServiceProvider();

        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<KanbanDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task Run(Func<DemoDataSeeder, Task> action)
    {
        await using var scope = _services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<DemoDataSeeder>());
    }

    private async Task<T> Query<T>(Func<KanbanDbContext, Task<T>> query)
    {
        await using var scope = _services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<KanbanDbContext>());
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        await Run(s => s.SeedAsync(Password));
        await Run(s => s.SeedAsync(Password));

        Assert.Equal(1, await Query(db => db.Users.CountAsync()));
        Assert.Equal(1, await Query(db => db.Boards.CountAsync()));
        Assert.True(await Query(db => db.Cards.CountAsync()) > 0);
    }

    [Fact]
    public async Task Reset_restores_original_data()
    {
        await Run(s => s.SeedAsync(Password));
        var originalCards = await Query(db => db.Cards.CountAsync());

        await Query(async db =>
        {
            await db.Cards.Take(2).ExecuteDeleteAsync();
            return 0;
        });
        await Run(s => s.ResetAsync(Password));

        Assert.Equal(1, await Query(db => db.Boards.CountAsync()));
        Assert.Equal(originalCards, await Query(db => db.Cards.CountAsync()));
    }
}
