# BookCatalog API

A .NET 10 REST API that exposes a searchable, paginated catalogue of books and authors
sourced from [Wolne Lektury](https://wolnelektury.pl) (an open Polish digital library).
A background job syncs the catalogue automatically on startup and daily at 3 AM.

## Tech Stack

| Concern | Library |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| Database | SQL Server 2022 + EF Core 10 |
| Background jobs | TickerQ |
| Structured logging | Serilog + Serilog.Sinks.OpenTelemetry |
| Observability | OpenTelemetry (traces + metrics → OTLP) |
| API docs | Scalar / OpenAPI |
| Testing | NUnit + Testcontainers |

---

## Prerequisites

| Tool | Minimum version |
|---|---|
| Docker + Docker Compose | Docker 24 |
| .NET SDK | 10.0 (only for local dev / running tests) |

---

## Quick Start

```bash
git clone <repo-url>
cd BookCatalog
docker compose up --build
```

The API will be available at **http://localhost:8080**.

On first start the database schema is created automatically (`EnsureCreated`) and the
background sync job fetches the full catalogue from Wolne Lektury. This may take a minute
or two before data appears in the endpoints.

> **SA password** — the default password (`MyStrong@Password1`) is intentionally simple
> for local development. Change it in `docker-compose.yml` before any public deployment.

---

## API Endpoints

All responses use **snake_case** JSON.

**Base address when running via Docker Compose:**
```
http://localhost:8080
```
> For local dev without Docker Compose the base address is `http://localhost:5000`.

### `GET /authors`

Returns a paginated list of authors.

| Parameter | Default | Description |
|---|---|---|
| `page` | `1` | Page number |
| `page_size` | `20` | Items per page |
| `sort_by` | `name:asc` | `name:asc` or `name:desc` |

**Example**
```
GET /authors?page=1&page_size=5&sort_by=name:desc
```

---

### `GET /books`

Returns a paginated, filterable list of books.

| Parameter | Default | Description |
|---|---|---|
| `page` | `1` | Page number |
| `page_size` | `20` | Items per page |
| `sort_by` | `title:asc` | `title:asc`, `title:desc`, `author:asc`, `author:desc` |
| `author` | _(none)_ | Filter by author slug |
| `epoch` | _(none)_ | Filter by epoch tag slug |
| `kind` | _(none)_ | Filter by kind tag slug |
| `genre` | _(none)_ | Filter by genre tag slug |

**Example**
```
GET /books?author=adam-mickiewicz&sort_by=title:asc
```

---

### `GET /books/{slug}`

Returns a single book by slug, including its linked authors.
Returns **404** if not found.

**Example**
```
GET /books/pan-tadeusz
```

---

### Internal endpoints

| Path | Description |
|---|---|
| `POST /internal/api/sync` | Manually trigger a full catalog sync |

#### `POST /internal/api/sync`

Manually triggers the full catalog sync workflow when the automatic daily job is
not sufficient (e.g. after a data fix, during initial setup, or to force an
immediate refresh). Enqueues the same two-step chain as the scheduled job:

1. **CatalogSync** — rebuilds the book catalogue from Wolne Lektury
2. **AuthorsSync** — backfills any author sort keys still missing (runs only on success of step 1)

Returns `202 Accepted` once the job chain has been enqueued. Processing happens
asynchronously in the background.

**Example**
```
POST /internal/api/sync
```

---

### Development-only endpoints

Available when `ASPNETCORE_ENVIRONMENT=Development`:

| Path | Description |
|---|---|
| `GET /openapi/v1.json` | Raw OpenAPI specification |
| `GET /scalar/v1` | Scalar interactive API UI |

#### Enabling dev mode in Docker Compose

By default `docker-compose.yml` runs the API in `Production` mode. To enable the
Scalar UI and the raw OpenAPI spec, override the environment variable:

```bash
ASPNETCORE_ENVIRONMENT=Development docker compose up --build
```

or edit `docker-compose.yml` directly:

```yaml
environment:
  ASPNETCORE_ENVIRONMENT: Development
```

Once running in Development mode the Scalar interactive UI is available at:

```
http://localhost:8080/scalar/v1
```

---

## Running Locally (without Docker Compose)

> Requires .NET 10 SDK and Docker (for the database container).

**1. Start SQL Server**

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=MyStrong@Password1" \
  -p 1433:1433 --name sql2022 --hostname sql2022 -d \
  mcr.microsoft.com/mssql/server:2022-latest
```

**2. Run the API**

```bash
dotnet run --project BookCatalog.Api
```

The API starts at `https://localhost:5001` (HTTP `http://localhost:5000`).
Scalar UI is available at `/scalar/v1`.

---

## Running Tests

> Tests use **Testcontainers** — Docker must be running on the host machine.
> Each test fixture spins up its own isolated SQL Server container; the docker-compose
> stack does **not** need to be running.

```bash
# Run the entire test suite
dotnet test

# Run only ApplicationCore tests (unit + handler + job tests)
dotnet test BookCatalog.ApplicationCore.Tests

# Run only API integration tests (full HTTP stack)
dotnet test BookCatalog.Api.Tests
```

Test counts:

| Project | Tests |
|---|---|
| `BookCatalog.ApplicationCore.Tests` | 99 |
| `BookCatalog.Api.Tests` | 16 |

---

## Project Structure

```
BookCatalog/
├── BookCatalog.Api/                  # ASP.NET Core entry point
│   ├── Extensions/
│   │   ├── SerilogExtensions.cs      # AddSerilogLogging, UseStructuredRequestLogging
│   │   └── OpenTelemetryExtensions.cs # AddObservability
│   ├── Public/
│   │   ├── AuthorsController.cs      # GET /authors
│   │   └── BooksController.cs        # GET /books, GET /books/{slug}
│   ├── Internal/
│   │   └── SyncTriggerController.cs  # POST /internal/api/sync
│   ├── GlobalExceptionHandler.cs
│   ├── Program.cs
│   └── appsettings*.json
│
├── BookCatalog.ApplicationCore/      # Domain logic
│   ├── Helpers/                      # Paging, PagedResult, SortDescriptor
│   ├── Jobs/                         # CatalogSyncJob, RemainingSyncAuthorsDataSyncJob, WorkflowJobDefinitions
│   ├── QueryHandlers/                # AuthorsHandler, BooksHandler
│   └── DependencyInjection/          # ApplicationCoreExtensions
│
├── BookCatalog.Persistence/          # EF Core DbContext + entity models
│
├── BookCatalog.Integrations/         # Wolne Lektury HTTP client
│
├── BookCatalog.Api.Tests/            # API integration tests (Testcontainers)
├── BookCatalog.ApplicationCore.Tests/ # Unit + handler + job tests (Testcontainers)
│
├── Dockerfile
├── docker-compose.yml
└── BookCatalog.sln
```

---

## Configuration

All settings can be overridden via environment variables using the standard .NET
`__` double-underscore separator for nested keys.

### Connection String

```
ConnectionStrings__BookCatalogDbContext=Data Source=<host>,1433;Initial Catalog=<db>;User Id=SA;Password=<pw>;TrustServerCertificate=True;MultipleActiveResultSets=true
```

### OpenTelemetry (traces + metrics)

The OTLP exporter reads the standard OpenTelemetry environment variables:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://your-collector:4317
OTEL_SERVICE_NAME=BookCatalog.Api          # optional override
OTEL_RESOURCE_ATTRIBUTES=env=production    # optional extra resource tags
```

When no collector is configured the exporter degrades gracefully (drops spans/metrics
without affecting the application).

### Serilog OTLP log sink

```
Serilog__WriteTo__1__Args__endpoint=http://your-collector:4318/v1/logs
```

### Log levels

```
Serilog__MinimumLevel__Default=Information
Serilog__MinimumLevel__Override__Microsoft=Warning
```

---

## Observability

| Signal | Transport | Notes |
|---|---|---|
| Logs | Console (structured JSON) + OTLP | CompactJsonFormatter in production, plain text in Development |
| Traces | OTLP | ASP.NET Core request + outbound HTTP client spans |
| Metrics | OTLP | ASP.NET Core + HTTP client + .NET runtime metrics |

In `Development` mode the console exporter is also enabled for traces and metrics,
making it easy to inspect telemetry without a collector.

Compatible backends: **Jaeger**, **Grafana Tempo + Loki + Mimir** (LGTM stack),
**Zipkin**, **OpenTelemetry Collector**, or any OTLP-capable receiver.

To add a local Grafana stack, append the following to `docker-compose.yml`:

```yaml
  otel-collector:
    image: grafana/alloy:latest
    # ... mount your alloy config
```

---

## License

This project is provided as a demo/learning resource. Feel free to adapt it.
