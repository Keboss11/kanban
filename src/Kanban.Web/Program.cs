using Kanban.Web;
using Kanban.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// En producción, solo avisos y errores en la consola del navegador (no cada petición HTTP).
if (!builder.HostEnvironment.IsDevelopment())
    builder.Logging.SetMinimumLevel(LogLevel.Warning);

var apiBase = new Uri(builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress);

builder.Services.AddAuthorizationCore();
// Singletons: los handlers de IHttpClientFactory viven en otro scope y deben ver la misma sesión.
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, KanbanAuthStateProvider>();
builder.Services.AddTransient<AuthHeaderHandler>();
builder.Services.AddScoped<KanbanApiClient>();
builder.Services.AddSingleton(sp => new BoardRealtime(sp.GetRequiredService<AuthService>(), apiBase));

builder.Services.AddHttpClient(HttpClients.Auth, c => c.BaseAddress = apiBase);
builder.Services.AddHttpClient(HttpClients.Api, c => c.BaseAddress = apiBase)
    .AddHttpMessageHandler<AuthHeaderHandler>();

await builder.Build().RunAsync();
