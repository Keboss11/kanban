using System.Text.Json.Serialization;
using Kanban.Api.Auth;
using Kanban.Api.Errors;
using Kanban.Api.Services;
using Kanban.Infrastructure;
using Kanban.Infrastructure.Identity;
using Kanban.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<KanbanExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(o => o.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<KanbanDbContext>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddScoped<BoardAccess>();
builder.Services.AddScoped<BoardService>();
builder.Services.AddScoped<CardService>();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Configuration);

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();

app.Run();

/// <summary>Expuesto para WebApplicationFactory en los tests de integración.</summary>
public partial class Program;
