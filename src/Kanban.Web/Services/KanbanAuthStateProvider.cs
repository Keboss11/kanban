using System.Net.Http.Json;
using System.Security.Claims;
using Kanban.Shared.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace Kanban.Web.Services;

/// <summary>Estado de sesión: si hay token, pregunta a /auth/me quién es el usuario.</summary>
public sealed class KanbanAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly AuthService _auth;
    private readonly IHttpClientFactory _httpFactory;
    private Task<AuthenticationState>? _current;

    public KanbanAuthStateProvider(AuthService auth, IHttpClientFactory httpFactory)
    {
        _auth = auth;
        _httpFactory = httpFactory;
        _auth.Changed += OnChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _current ??= LoadAsync();

    private async Task<AuthenticationState> LoadAsync()
    {
        if (!await _auth.IsLoggedInAsync())
            return Anonymous;

        try
        {
            var me = await _httpFactory.CreateClient(HttpClients.Api).GetFromJsonAsync<UserInfoDto>("auth/me");
            if (me is null)
                return Anonymous;

            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, me.Id),
                new Claim(ClaimTypes.Name, me.DisplayName),
                new Claim(ClaimTypes.Email, me.Email),
            ], authenticationType: "bearer");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (HttpRequestException)
        {
            return Anonymous;
        }
    }

    private void OnChanged()
    {
        _current = LoadAsync();
        NotifyAuthenticationStateChanged(_current);
    }

    public void Dispose() => _auth.Changed -= OnChanged;
}
