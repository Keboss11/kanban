using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace Kanban.Tests.Api;

/// <summary>
/// API completa con una base de datos propia por fábrica. Los tests se aíslan registrando usuarios distintos.
/// Por defecto usa SQLite en memoria; si existe la variable KANBAN_TEST_POSTGRES (cadena de conexión a un
/// servidor PostgreSQL), crea ahí una base desechable, igual que en producción.
/// </summary>
public sealed class KanbanApiFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly string? PostgresServer = Environment.GetEnvironmentVariable("KANBAN_TEST_POSTGRES");

    private readonly string _provider;
    private readonly string _connectionString;
    private readonly SqliteConnection? _keepAlive;

    public KanbanApiFactory()
    {
        if (PostgresServer is { Length: > 0 })
        {
            _provider = "Postgres";
            _connectionString = new NpgsqlConnectionStringBuilder(PostgresServer) { Database = $"kanban_test_{Guid.NewGuid():N}" }.ConnectionString;
        }
        else
        {
            _provider = "Sqlite";
            _connectionString = $"Data Source=kanban-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            // La base en memoria compartida vive mientras quede alguna conexión abierta.
            _keepAlive = new SqliteConnection(_connectionString);
            _keepAlive.Open();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", _provider);
        builder.UseSetting("ConnectionStrings:Kanban", _connectionString);
    }

    /// <summary>Registra un usuario nuevo y devuelve un cliente autenticado con su token bearer.</summary>
    public async Task<HttpClient> CreateUserClientAsync(string? email = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";
        const string password = "Test1234!";
        var client = CreateClient();

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _keepAlive?.Dispose();
        if (_provider == "Postgres")
            DropPostgresDatabase();
    }

    private void DropPostgresDatabase()
    {
        var database = new NpgsqlConnectionStringBuilder(_connectionString).Database;
        NpgsqlConnection.ClearAllPools();
        using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(PostgresServer) { Database = "postgres" }.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)";
        command.ExecuteNonQuery();
    }
}
