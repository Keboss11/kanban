using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kanban.Infrastructure.Persistence;

/// <summary>
/// SQLite en local y PostgreSQL (Neon) en producción. Cada proveedor tiene sus migraciones:
/// las de SQLite en este proyecto y las de PostgreSQL en Kanban.Migrations.Postgres.
/// </summary>
public static class DatabaseProvider
{
    public const string Sqlite = "Sqlite";
    public const string Postgres = "Postgres";
    public const string PostgresMigrationsAssembly = "Kanban.Migrations.Postgres";

    public static void Configure(DbContextOptionsBuilder options, string provider, string connectionString)
    {
        if (provider.Equals(Postgres, StringComparison.OrdinalIgnoreCase))
            options.UseNpgsql(ToNpgsql(connectionString), o => o.MigrationsAssembly(PostgresMigrationsAssembly));
        else if (provider.Equals(Sqlite, StringComparison.OrdinalIgnoreCase))
            options.UseSqlite(connectionString);
        else
            throw new InvalidOperationException($"Proveedor de base de datos desconocido: «{provider}». Usa {Sqlite} o {Postgres}.");
    }

    /// <summary>Acepta tanto el formato de Npgsql como la URL postgresql://usuario:clave@host/db que da Neon.</summary>
    public static string ToNpgsql(string connectionString)
    {
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            SslMode = SslMode.Require,
        };

        // Parámetros de la URL (?sslmode=require&channel_binding=require…), en el formato de Npgsql.
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]).Replace("_", " ");
            if (kv.Length == 2 && builder.ContainsKey(key))
                builder[key] = Uri.UnescapeDataString(kv[1]);
        }
        return builder.ConnectionString;
    }
}
