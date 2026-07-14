# Qwiik Invoices API

A small, multi-tenant **invoice management** backend for a SaaS product — lets a customer manage invoices for their organization.

The focus is a clean, well-modelled core with explicit trade-offs, and a clear statement of what is intentionally out of scope.

## Stack

- C# / .NET 10
- ASP.NET Core Web API
- Entity Framework Core (SQL Server / LocalDB)
- xUnit for tests

## Endpoints

| Verb  | Route                          | Purpose                       |
|-------|--------------------------------|-------------------------------|
| POST  | `/api/v1/invoices`             | Create an invoice             |
| GET   | `/api/v1/invoices`             | List invoices (paged/filtered)|
| GET   | `/api/v1/invoices/{id}`        | View invoice details          |
| PATCH | `/api/v1/invoices/{id}/status` | Update invoice status         |
| GET   | `/api/v1/invoices/summary`     | Invoice summary / dashboard   |

Every request carries the tenant via an `X-Tenant-Id` header (a deliberate stub for a
validated identity claim — see [`SOLUTION_NOTES.md`](SOLUTION_NOTES.md)).

## Getting started

### Docker Compose (one command)

```bash
docker compose up --build
```

Brings up SQL Server, applies the EF migration on startup, and serves the API.

Quick smoke test once it's up — `GET http://localhost:8080/health/ready` should return
`200` (API up + DB check passed). It's block `### 0` in [`requests.http`](requests.http).

### Local dotnet (LocalDB)

```bash
dotnet build
dotnet run --project src/Qwiik.Invoices.Api
```

In `Development` this opens an interactive **Scalar** API reference at `/scalar`
(the OpenAPI document is served at `/openapi/v1.json`) and migrates on startup, so a
`dotnet run` against an empty database self-heals.

Health: `GET /health` (liveness), `GET /health/ready` (readiness — includes the DB check).

### Tests

```bash
dotnet test
```

Integration tests run against a real SQL Server — Testcontainers by default (Docker
running), or set `QWIIK_TEST_SQL` to a LocalDB/SQL Server connection string as a fallback.

CI (GitHub Actions) builds in Release and runs the full suite on every push and PR to
`main`, using Testcontainers on the runner's Docker daemon.

### Trying the endpoints

[`requests.http`](requests.http) is a runnable set covering all 5 endpoints plus a
missing-tenant example; run the blocks top to bottom. The **/scalar** UI can also send
requests interactively.

## Documentation

- [`SOLUTION_NOTES.md`](SOLUTION_NOTES.md) — architecture, design decisions, trade-offs
- [`AI_USAGE.md`](AI_USAGE.md) — how AI was used in building this
- [`CLAUDE.md`](CLAUDE.md) — architecture rules and guardrails
