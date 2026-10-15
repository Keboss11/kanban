using System.Net;
using System.Net.Http.Headers;

namespace Kanban.Web.Services;

/// <summary>Añade el token bearer a las peticiones a la API y cierra sesión si la API lo rechaza.</summary>
public sealed class AuthHeaderHandler(AuthService auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (await auth.GetAccessTokenAsync() is { } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized && request.Headers.Authorization is not null)
            await auth.LogoutAsync();
        return response;
    }
}

public static class HttpClients
{
    /// <summary>Sin el handler de auth (login, registro, refresh).</summary>
    public const string Auth = "auth";

    /// <summary>Con token bearer.</summary>
    public const string Api = "api";
}
