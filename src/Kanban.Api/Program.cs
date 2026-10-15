using Kanban.Infrastructure;
using Kanban.Infrastructure.Identity;
using Kanban.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<KanbanDbContext>();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Configuration);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

/// <summary>Expuesto para WebApplicationFactory en los tests de integración.</summary>
public partial class Program;
