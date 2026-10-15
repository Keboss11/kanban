# Kanban + MCP (.NET)

Tablero Kanban con tiempo real y un servidor **MCP** integrado, para que Claude pueda crear, mover y comentar tarjetas pasando por las mismas reglas que un usuario.

> 🚧 En construcción. Fase actual: **F0 – Base**.

## Stack

- **Backend:** ASP.NET Core 9 (REST + SignalR + endpoint MCP), EF Core, ASP.NET Core Identity
- **Frontend:** Blazor WebAssembly (PWA)
- **Base de datos:** SQLite en local, PostgreSQL (Neon) en producción
- **Tests:** xUnit + `WebApplicationFactory`

## Estructura

```
src/
  Kanban.Domain          → entidades y reglas
  Kanban.Infrastructure  → EF Core, migraciones, repositorios
  Kanban.Api             → REST + SignalR hub + endpoint MCP + auth
  Kanban.Web             → Blazor WebAssembly (PWA)
  Kanban.Shared          → DTOs compartidos entre Api y Web
tests/
  Kanban.Tests           → xUnit (unitarios + integración)
```

## Desarrollo local

Requisitos: [.NET SDK 9](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/Kanban.Api
dotnet run --project src/Kanban.Web
```

## Hoja de ruta

- [x] F0 – Base: repositorio, solución, CI
- [ ] F1 – Dominio + base de datos
- [ ] F2 – API REST + autenticación
- [ ] F3 – Interfaz Blazor
- [ ] F4 – Tiempo real (SignalR)
- [ ] F5 – Servidor MCP
- [ ] F6 – Despliegue
- [ ] F7 – PWA + móvil
- [ ] F8 – OAuth para el MCP
- [ ] F9 – Escaparate
