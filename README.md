# Kanban + MCP (.NET)

Tablero Kanban con tiempo real y un servidor **MCP** integrado, para que Claude pueda crear, mover y comentar tarjetas pasando por las mismas reglas que un usuario.

> 🚧 En construcción. Fase actual: **F6 – Despliegue**.

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

API en `http://localhost:5072` y web en `http://localhost:5071` (la URL de la API se configura en `src/Kanban.Web/wwwroot/appsettings.json`).

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

## Interfaz web

Blazor WebAssembly con CSS propio (modo claro/oscuro, responsive) en lugar de una librería de componentes.

- Login y registro con tokens bearer guardados en `localStorage` y renovados antes de caducar.
- Tablero con columnas, límite WIP, tarjetas con prioridad, etiquetas y fecha límite.
- **Arrastrar y soltar** con SortableJS (ratón y dedo) y menú **«Mover a…»** como alternativa accesible.
- Modal de tarjeta con descripción en Markdown (sin HTML y solo enlaces seguros) y comentarios.
- Historial del tablero: los cambios hechos por IA aparecen como «🤖 Claude …».

## Tiempo real

Hub de SignalR en `/hubs/board` con **un grupo por tablero** (solo el propietario puede unirse).
Los servicios de la API emiten `BoardChanged` al guardar, con el mismo resumen que el historial,
así que cualquier cambio, venga de la web o de Claude vía MCP, aparece al momento en todas las pestañas abiertas.

- El token viaja en `?access_token=` porque los WebSockets del navegador no admiten cabeceras.
- Cada petición de la web incluye `X-SignalR-Connection`; esa pestaña no recibe el eco de su propio cambio.
- Al reconectar, el cliente vuelve a unirse al grupo y recarga el tablero.

## Servidor MCP

La propia API expone un endpoint **MCP** en `/mcp` (SDK oficial `ModelContextProtocol.AspNetCore`, Streamable HTTP, sin estado).
Las herramientas llaman a **los mismos servicios** que la web: mismas validaciones, mismo control de acceso, mismo historial
(marcado como «🤖 Claude») y mismos avisos en tiempo real.

| Herramienta | Qué hace |
|---|---|
| `list_boards` | Tableros del usuario |
| `get_board` | Columnas y tarjetas de un tablero |
| `create_board` | Crear un tablero con columnas por defecto |
| `create_card` / `update_card` / `delete_card` | CRUD de tarjetas (`update_card` solo cambia lo indicado) |
| `move_card` | Cambiar de columna (por nombre o id) o de posición |
| `add_comment` | Comentar una tarjeta |
| `search_cards` | Buscar por texto, etiqueta o prioridad |

Prompts: **`planificar_feature`** (divide una idea en tarjetas y las crea tras confirmarlo) y
**`resumen_estado`** (bloqueadas, vencidas y avances de la semana, calculados en el servidor).

### Conectarlo a Claude Code

1. En la web, **Ajustes → Crear token** (se muestra una sola vez; en la base de datos solo se guarda su hash).
2. Copia el comando que aparece, o añádelo a mano:

```bash
claude mcp add --transport http kanban http://localhost:5072/mcp --header "Authorization: Bearer kbn_..."
```

Este repositorio incluye un `.mcp.json` que lee el token de la variable de entorno `KANBAN_MCP_TOKEN`,
así que basta con definirla y abrir Claude Code en la carpeta del proyecto.

Los tokens personales solo sirven para `/mcp` (no para la API REST) y todo lo que se hace con ellos queda registrado como hecho por IA.

## Despliegue (0 €)

| Pieza | Dónde | Cómo |
|---|---|---|
| Base de datos | **Neon** (PostgreSQL) | Migraciones propias en `Kanban.Migrations.Postgres`; la API las aplica al arrancar |
| API | **Render** (Docker, plan gratuito) | `Dockerfile` + `render.yaml` (Blueprint) |
| Web | **GitHub Pages** | Workflow `pages.yml`: `base href` `/kanban/` y URL de la API antes de publicar |
| Demo | GitHub Actions | `demo-reset.yml` reinicia la cuenta demo cada noche (03:00 UTC) |

Decisiones relevantes:

- **Claves de Data Protection en la base de datos**: el contenedor de Render es efímero y se duerme; con las claves en disco cada reinicio cerraría todas las sesiones.
- **CI contra PostgreSQL**: los tests de la API se ejecutan también contra `postgres:17` (además de SQLite) y se construye la imagen Docker en cada push.
- **Cuenta demo pública** (`demo@kanban.local` / `Demo1234!`): no puede cambiar su contraseña, email ni 2FA, y sus datos y tokens MCP se reinician cada noche.

### Puesta en marcha

1. **Neon**: crea un proyecto y copia la cadena de conexión (`postgresql://…?sslmode=require`).
2. **Render**: *New → Blueprint* con este repositorio. Pega la cadena de Neon en `ConnectionStrings__Kanban`.
3. **GitHub** (*Settings → Secrets and variables → Actions*):
   - variable `API_URL` = URL del servicio de Render (p. ej. `https://kanban-api.onrender.com`);
   - secreto `DEMO_RESET_KEY` = valor de `Demo__ResetKey` que generó Render.
4. **GitHub Pages**: *Settings → Pages → Source: GitHub Actions* y relanza el workflow *Web (GitHub Pages)*.

## Hoja de ruta

- [x] F0 – Base: repositorio, solución, CI
- [x] F1 – Dominio + base de datos
- [x] F2 – API REST + autenticación
- [x] F3 – Interfaz Blazor
- [x] F4 – Tiempo real (SignalR)
- [x] F5 – Servidor MCP
- [ ] F6 – Despliegue
- [ ] F7 – PWA + móvil
- [ ] F8 – OAuth para el MCP
- [ ] F9 – Escaparate
