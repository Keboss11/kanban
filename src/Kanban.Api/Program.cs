using System.Text.Json.Serialization;
using Kanban.Api.Auth;
using Kanban.Api.Endpoints;
using Kanban.Api.Errors;
using Kanban.Api.Realtime;
using Kanban.Api.Services;
using Kanban.Infrastructure;
using Kanban.Infrastructure.Identity;
using Kanban.Infrastructure.Persistence;
using Kanban.Shared.Realtime;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<KanbanExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddInfrastructure(builder.Configuration);

// La web (Blazor WASM) se sirve desde otro origen; sin cookies, así que no hace falta AllowCredentials.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<KanbanDbContext>()
    .AddErrorDescriber<SpanishIdentityErrorDescriber>();
// Los WebSockets del navegador no envían cabeceras: para el hub el token llega en ?access_token=.
builder.Services.Configure<BearerTokenOptions>(IdentityConstants.BearerScheme, o =>
    o.Events.OnMessageReceived = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments(BoardHubContract.Path) && ctx.Request.Query["access_token"] is [{ } token])
            ctx.Token = token;
        return Task.CompletedTask;
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddScoped<BoardAccess>();
builder.Services.AddScoped<BoardService>();
builder.Services.AddScoped<CardService>();

builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Configuration);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapBoardEndpoints();
app.MapCardEndpoints();
app.MapHub<BoardHub>(BoardHubContract.Path);

app.Run();

/// <summary>Expuesto para WebApplicationFactory en los tests de integración.</summary>
public partial class Program;
