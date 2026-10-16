using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kanban.Api.Auth;
using Kanban.Infrastructure.Persistence.Seeding;
using Kanban.Shared.Boards;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kanban.Tests.Api;

public sealed class DemoTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    private const string DemoPassword = "Demo1234!";
    private const string ResetKey = "clave-de-prueba";

    /// <summary>La misma API y base de datos, pero con la cuenta demo y la clave de reinicio configuradas.</summary>
    private WebApplicationFactory<Program> WithDemo() =>
        factory.WithWebHostBuilder(b => b
            .UseSetting("Seed:DemoPassword", DemoPassword)
            .UseSetting("Demo:ResetKey", ResetKey));

    private static async Task<HttpClient> LoginDemoAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/auth/login", new { email = DemoDataSeeder.DemoEmail, password = DemoPassword });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static HttpRequestMessage Reset(string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/demo/reset");
        if (key is not null)
            request.Headers.Add(DemoEndpoints.ResetKeyHeader, key);
        return request;
    }

    [Fact]
    public async Task Reset_endpoint_does_not_exist_without_configured_key()
    {
        var response = await factory.CreateClient().SendAsync(Reset(ResetKey));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reset_requires_the_right_key_and_restores_demo_data()
    {
        using var app = WithDemo();
        var demo = await LoginDemoAsync(app);
        var boards = await (await demo.GetAsync("/api/boards")).ReadAsync<List<BoardSummaryDto>>();
        var seeded = Assert.Single(boards);
        await demo.DeleteAsync($"/api/boards/{seeded.Id}");

        var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(Reset("otra"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(Reset(null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.SendAsync(Reset(ResetKey))).StatusCode);

        var after = await LoginDemoAsync(app);
        var restored = Assert.Single(await (await after.GetAsync("/api/boards")).ReadAsync<List<BoardSummaryDto>>());
        Assert.Equal(seeded.Name, restored.Name);
    }

    [Fact]
    public async Task Demo_account_credentials_cannot_be_changed()
    {
        using var app = WithDemo();
        var demo = await LoginDemoAsync(app);

        var response = await demo.PostAsJsonAsync("/auth/manage/info", new { newPassword = "Otra1234!", oldPassword = DemoPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await LoginDemoAsync(app); // la contraseña sigue siendo la misma
    }

    [Fact]
    public async Task Regular_users_can_still_manage_their_account()
    {
        var user = await factory.CreateUserClientAsync();

        var response = await user.PostAsJsonAsync("/auth/manage/info", new { newPassword = "Nueva1234!", oldPassword = "Test1234!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
