# Kanban + MCP (.NET)

Tablero Kanban con tiempo real y un servidor **MCP** integrado, para que Claude pueda crear, mover y comentar tarjetas pasando por las mismas reglas que un usuario.

> 🚧 En construcción. Fase actual: **F3 – Interfaz Blazor**.

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

En desarrollo la API crea `kanban.db` (SQLite), aplica las migraciones y siembra un usuario demo:
`demo@kanban.local` / `Demo1234!`.

Nueva migración:

```bash
dotnet tool restore
dotnet ef migrations add <Nombre> -p src/Kanban.Infrastructure -s src/Kanban.Infrastructure -o Persistence/Migrations
```

## API

Autenticación con ASP.NET Core Identity y **tokens bearer** (`/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/me`).
Todas las rutas `/api` requieren token. Un tablero ajeno responde **404**, igual que uno inexistente.
Los errores de validación se devuelven como `ProblemDetails` (400).

| Método | Ruta | Descripción |
|---|---|---|
| GET / POST | `/api/boards` | Listar / crear tableros (con columnas por defecto) |
| GET / PUT / DELETE | `/api/boards/{id}` | Tablero con columnas y tarjetas |
| GET | `/api/boards/{id}/activity` | Historial (origen Web / IA) |
| POST | `/api/boards/{id}/columns` | Añadir columna (límite WIP opcional) |
| PUT / DELETE | `/api/boards/{id}/columns/{columnId}` | Editar / borrar columna vacía |
| POST | `/api/boards/{id}/columns/{columnId}/move` | Reordenar columna |
| POST | `/api/boards/{id}/cards` | Crear tarjeta |
| GET / PUT / DELETE | `/api/boards/{id}/cards/{cardId}` | Tarjeta |
| POST | `/api/boards/{id}/cards/{cardId}/move` | Mover a columna e índice |
| GET / POST | `/api/boards/{id}/cards/{cardId}/comments` | Comentarios |
| GET | `/api/cards/search?text=&label=&priority=&boardId=` | Búsqueda en tus tableros |

## Hoja de ruta

- [x] F0 – Base: repositorio, solución, CI
- [x] F1 – Dominio + base de datos
- [x] F2 – API REST + autenticación
- [ ] F3 – Interfaz Blazor
- [ ] F4 – Tiempo real (SignalR)
- [ ] F5 – Servidor MCP
- [ ] F6 – Despliegue
- [ ] F7 – PWA + móvil
- [ ] F8 – OAuth para el MCP
- [ ] F9 – Escaparate
