using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Kanban.Tests.Api;

/// <summary>
/// API completa con SQLite en memoria (una base por fábrica). Los tests se aíslan
/// registrando usuarios distintos, no recreando la base.
/// </summary>
public sealed class KanbanApiFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _connectionString = $"Data Source=kanban-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _keepAlive;

    public KanbanApiFactory()
    {
        // La base en memoria compartida vive mientras quede alguna conexión abierta.
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
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
        if (disposing)
            _keepAlive.Dispose();
    }
}
