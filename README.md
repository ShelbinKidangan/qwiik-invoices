# Qwiik Invoices API

A small, multi-tenant **invoice management** backend for a SaaS product — lets a customer manage invoices for their organization.

Built for the Qwiik Head of Engineering assessment. Optimized for **engineering judgment over volume**: a clean core, explicit trade-offs, and a clear statement of what was deliberately left out.

## Stack

- C# / .NET 10
- ASP.NET Core Web API
- Entity Framework Core (SQL Server / LocalDB)
- xUnit for tests

## Endpoints (planned)

| Verb  | Route                          | Purpose                       |
|-------|--------------------------------|-------------------------------|
| POST  | `/api/v1/invoices`             | Create an invoice             |
| GET   | `/api/v1/invoices`             | List invoices (paged/filtered)|
| GET   | `/api/v1/invoices/{id}`        | View invoice details          |
| PATCH | `/api/v1/invoices/{id}/status` | Update invoice status         |
| GET   | `/api/v1/invoices/summary`     | Invoice summary / dashboard   |

## Getting started

> _Run instructions land as the solution is built. Quickstart (Docker Compose + `dotnet run`) and sample requests will be documented here._

```bash
dotnet build
dotnet run --project src/Qwiik.Invoices.Api
```

## Documentation

- [`SOLUTION_NOTES.md`](SOLUTION_NOTES.md) — architecture, design decisions, trade-offs _(added later)_
- [`AI_USAGE.md`](AI_USAGE.md) — how AI was used in building this
- [`CLAUDE.md`](CLAUDE.md) — architecture rules and guardrails
