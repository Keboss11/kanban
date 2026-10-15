using Kanban.Web;
using Kanban.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = new Uri(builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress);

builder.Services.AddAuthorizationCore();
// Singletons: los handlers de IHttpClientFactory viven en otro scope y deben ver la misma sesión.
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, KanbanAuthStateProvider>();
builder.Services.AddTransient<AuthHeaderHandler>();
builder.Services.AddScoped<KanbanApiClient>();

builder.Services.AddHttpClient(HttpClients.Auth, c => c.BaseAddress = apiBase);
builder.Services.AddHttpClient(HttpClients.Api, c => c.BaseAddress = apiBase)
    .AddHttpMessageHandler<AuthHeaderHandler>();

await builder.Build().RunAsync();
