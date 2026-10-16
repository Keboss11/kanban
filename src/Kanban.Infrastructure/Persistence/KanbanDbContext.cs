using Kanban.Domain.Activity;
using Kanban.Domain.Boards;
using Kanban.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kanban.Infrastructure.Persistence;

public sealed class KanbanDbContext(DbContextOptions<KanbanDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<Column> Columns => Set<Column>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();

    /// <summary>
    /// Claves que firman los tokens bearer. En la base de datos y no en disco: el contenedor de producción
    /// se reinicia (y se duerme) a menudo y, si se perdieran, se cerraría la sesión de todos los usuarios.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(KanbanDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums como texto: la base de datos es legible y reordenar el enum no corrompe datos.
        configurationBuilder.Properties<Priority>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ActivitySource>().HaveConversion<string>().HaveMaxLength(16);
        configurationBuilder.Properties<ActivityAction>().HaveConversion<string>().HaveMaxLength(32);

        // Todas las fechas se guardan en UTC; SQLite las devuelve sin Kind y se serializarían sin "Z".
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
