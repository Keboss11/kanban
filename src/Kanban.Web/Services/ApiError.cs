using System.Net.Http.Json;
using System.Text.Json;

namespace Kanban.Web.Services;

/// <summary>Error devuelto por la API, con el mensaje ya listo para mostrar.</summary>
public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class ApiError
{
    /// <summary>Extrae un mensaje legible de un ProblemDetails / ValidationProblem.</summary>
    public static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (body.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(e => e.Value.EnumerateArray().Select(m => m.GetString()))
                    .OfType<string>()
                    .Distinct();
                return string.Join(" ", messages);
            }
            if (body.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } d)
                return d;
            if (body.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } t)
                return t;
        }
        catch (JsonException)
        {
        }
        return $"Error {(int)response.StatusCode}.";
    }
}
