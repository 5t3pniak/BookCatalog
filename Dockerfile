# ── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first — separate layer so restore is cached between source changes
COPY BookCatalog.sln global.json ./
COPY BookCatalog.Api/BookCatalog.Api.csproj                    BookCatalog.Api/
COPY BookCatalog.ApplicationCore/BookCatalog.ApplicationCore.csproj  BookCatalog.ApplicationCore/
COPY BookCatalog.Persistence/BookCatalog.Persistence.csproj          BookCatalog.Persistence/
COPY BookCatalog.Integrations/BookCatalog.Integrations.csproj        BookCatalog.Integrations/

RUN dotnet restore BookCatalog.Api/BookCatalog.Api.csproj

# Copy source and publish
COPY BookCatalog.Api/            BookCatalog.Api/
COPY BookCatalog.ApplicationCore/ BookCatalog.ApplicationCore/
COPY BookCatalog.Persistence/     BookCatalog.Persistence/
COPY BookCatalog.Integrations/    BookCatalog.Integrations/

RUN dotnet publish BookCatalog.Api/BookCatalog.Api.csproj \
    --no-restore -c Release -o /app/publish

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Run as the built-in non-root user provided by the .NET images
USER $APP_UID

ENTRYPOINT ["dotnet", "BookCatalog.Api.dll"]
