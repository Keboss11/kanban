using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kanban.Web.Services;

/// <summary>Login, registro y renovación de tokens contra los endpoints de MapIdentityApi.</summary>
public sealed class AuthService(IHttpClientFactory httpFactory, TokenStore store)
{
    /// <summary>Margen para renovar el token antes de que caduque.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private HttpClient Http => httpFactory.CreateClient(HttpClients.Auth);

    /// <summary>Se dispara al iniciar o cerrar sesión.</summary>
    public event Action? Changed;

    /// <returns>Mensaje de error, o null si ha ido bien.</returns>
    public async Task<string?> LoginAsync(string email, string password)
    {
        var response = await Http.PostAsJsonAsync("auth/login", new { email, password });
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return "Email o contraseña incorrectos.";
        if (!response.IsSuccessStatusCode)
            return await ApiError.ReadMessageAsync(response);

        await StoreAsync(response);
        Changed?.Invoke();
        return null;
    }

    /// <returns>Mensaje de error, o null si ha ido bien (y la sesión queda iniciada).</returns>
    public async Task<string?> RegisterAsync(string email, string password)
    {
        var response = await Http.PostAsJsonAsync("auth/register", new { email, password });
        if (!response.IsSuccessStatusCode)
            return await ApiError.ReadMessageAsync(response);

        return await LoginAsync(email, password);
    }

    public async Task LogoutAsync()
    {
        await store.ClearAsync();
        Changed?.Invoke();
    }

    public async Task<bool> IsLoggedInAsync() => await store.GetAsync() is not null;

    /// <summary>Token de acceso válido (renovándolo si está a punto de caducar), o null si no hay sesión.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        var tokens = await store.GetAsync();
        if (tokens is null)
            return null;
        if (tokens.ExpiresAt - RefreshMargin > DateTimeOffset.UtcNow)
            return tokens.AccessToken;

        await _refreshLock.WaitAsync();
        try
        {
            // Otra petición pudo renovarlo mientras esperábamos.
            tokens = await store.GetAsync();
            if (tokens is null)
                return null;
            if (tokens.ExpiresAt - RefreshMargin > DateTimeOffset.UtcNow)
                return tokens.AccessToken;

            var response = await Http.PostAsJsonAsync("auth/refresh", new { refreshToken = tokens.RefreshToken });
            if (!response.IsSuccessStatusCode)
            {
                await LogoutAsync();
                return null;
            }
            return (await StoreAsync(response)).AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<AuthTokens> StoreAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tokens = new AuthTokens(
            body.GetProperty("accessToken").GetString()!,
            body.GetProperty("refreshToken").GetString()!,
            DateTimeOffset.UtcNow.AddSeconds(body.GetProperty("expiresIn").GetInt32()));
        await store.SetAsync(tokens);
        return tokens;
    }
}
