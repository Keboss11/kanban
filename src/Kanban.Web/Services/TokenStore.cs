using System.Text.Json;
using Microsoft.JSInterop;

namespace Kanban.Web.Services;

public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>Guarda los tokens en localStorage (bearer, sin cookies: la API está en otro dominio).</summary>
public sealed class TokenStore(IJSRuntime js)
{
    private const string Key = "kanban.auth";

    private AuthTokens? _cached;
    private bool _loaded;

    public async ValueTask<AuthTokens?> GetAsync()
    {
        if (_loaded)
            return _cached;

        var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
        _cached = json is null ? null : JsonSerializer.Deserialize<AuthTokens>(json);
        _loaded = true;
        return _cached;
    }

    public async ValueTask SetAsync(AuthTokens tokens)
    {
        _cached = tokens;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(tokens));
    }

    public async ValueTask ClearAsync()
    {
        _cached = null;
        _loaded = true;
        await js.InvokeVoidAsync("localStorage.removeItem", Key);
    }
}
