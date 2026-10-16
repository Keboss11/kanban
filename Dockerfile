# API de Kanban (REST + SignalR + MCP). La web se publica aparte en GitHub Pages.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Primero solo los .csproj: la restauración queda en caché mientras no cambien las dependencias.
COPY global.json Directory.Build.props ./
COPY src/Kanban.Domain/Kanban.Domain.csproj src/Kanban.Domain/
COPY src/Kanban.Shared/Kanban.Shared.csproj src/Kanban.Shared/
COPY src/Kanban.Infrastructure/Kanban.Infrastructure.csproj src/Kanban.Infrastructure/
COPY src/Kanban.Migrations.Postgres/Kanban.Migrations.Postgres.csproj src/Kanban.Migrations.Postgres/
COPY src/Kanban.Api/Kanban.Api.csproj src/Kanban.Api/
RUN dotnet restore src/Kanban.Api/Kanban.Api.csproj

COPY src/ src/
RUN dotnet publish src/Kanban.Api/Kanban.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Database__Provider=Postgres
EXPOSE 8080

# Usuario sin privilegios que trae la imagen oficial.
USER $APP_UID
ENTRYPOINT ["dotnet", "Kanban.Api.dll"]
