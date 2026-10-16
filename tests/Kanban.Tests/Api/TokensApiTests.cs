using System.Net;
using System.Net.Http.Headers;
using Kanban.Api.Auth;
using Kanban.Shared.Auth;

namespace Kanban.Tests.Api;

public sealed class TokensApiTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    [Fact]
    public async Task Create_list_and_revoke_token()
    {
        var user = await factory.CreateUserClientAsync();

        var created = await (await user.PostJsonAsync("/api/tokens", new CreateTokenRequest("Claude Code")))
            .ReadAsync<CreatedTokenDto>(HttpStatusCode.Created);
        Assert.StartsWith("kbn_", created.Value);
        Assert.StartsWith(created.Token.Prefix, created.Value);

        var list = await (await user.GetAsync("/api/tokens")).ReadAsync<List<PersonalAccessTokenDto>>();
        var listed = Assert.Single(list);
        Assert.Equal("Claude Code", listed.Name);
        Assert.Null(listed.LastUsedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await user.DeleteAsync($"/api/tokens/{created.Token.Id}")).StatusCode);
        Assert.Empty(await (await user.GetAsync("/api/tokens")).ReadAsync<List<PersonalAccessTokenDto>>());
    }

    [Fact]
    public async Task Tokens_of_other_users_are_invisible_and_cannot_be_revoked()
    {
        var owner = await factory.CreateUserClientAsync();
        var other = await factory.CreateUserClientAsync();
        var created = await (await owner.PostJsonAsync("/api/tokens", new CreateTokenRequest("Mío")))
            .ReadAsync<CreatedTokenDto>(HttpStatusCode.Created);

        Assert.Empty(await (await other.GetAsync("/api/tokens")).ReadAsync<List<PersonalAccessTokenDto>>());
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/tokens/{created.Token.Id}")).StatusCode);
    }

    [Fact]
    public async Task Token_limit_and_name_are_validated()
    {
        var user = await factory.CreateUserClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostJsonAsync("/api/tokens", new CreateTokenRequest(" "))).StatusCode);

        for (var i = 0; i < TokenEndpoints.MaxTokensPerUser; i++)
            (await user.PostJsonAsync("/api/tokens", new CreateTokenRequest($"t{i}"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostJsonAsync("/api/tokens", new CreateTokenRequest("uno más"))).StatusCode);
    }

    [Fact]
    public async Task Personal_token_does_not_grant_access_to_rest_api()
    {
        var user = await factory.CreateUserClientAsync();
        var created = await (await user.PostJsonAsync("/api/tokens", new CreateTokenRequest("MCP")))
            .ReadAsync<CreatedTokenDto>(HttpStatusCode.Created);

        var withToken = factory.CreateClient();
        withToken.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", created.Value);

        Assert.Equal(HttpStatusCode.Unauthorized, (await withToken.GetAsync("/api/boards")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await withToken.GetAsync("/api/tokens")).StatusCode);
    }
}
