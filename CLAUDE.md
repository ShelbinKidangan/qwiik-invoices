# CLAUDE.md — Architecture rules & guardrails

Guidance for any contributor (human or AI) working in this repo. The overriding goal
of this assessment is **engineering judgment, not code volume**. The brief says
"small, clean, thoughtful." Keep it that way.

## What this is

A multi-tenant invoice management **module** — 5 endpoints, one bounded context.
Not an enterprise platform. A reviewer should understand the whole thing in ~10 minutes.

## Architecture

- **One API project + one test project.** That is enough for this scope.
- Layered-lite inside the API: `Domain/`, `Infrastructure/`, `Features/Invoices/`, `Common/`.
- EF Core's `DbContext` **is** the unit of work — do not wrap it in a repository.
- Business rules (status transitions, money totals) live in the **domain entities**,
  not in controllers or services.

## Do NOT build (over-engineering tells for a module this size)

- No Clean Architecture with 4+ projects.
- No CQRS, no MediatR, no microservices.
- No repository-or-generic-repository over EF.
- No AutoMapper for a handful of DTOs — map by hand.
- No real auth server, payments, PDF, email, webhooks, multi-currency FX.

If something is genuinely out of scope, we **name it in `SOLUTION_NOTES.md` §13**
rather than silently skipping it — or half-building it.

## Multi-tenancy (non-negotiable)

- Shared database, `TenantId` on every tenant-owned row.
- **EF Core global query filter** so no query can leak across tenants by construction.
- `TenantId` is stamped from the tenant context on write — **never** bound from the
  request body (no mass-assignment).

## Conventions

- File-scoped namespaces, nullable enabled, `AsNoTracking()` on reads, project to DTOs.
- Money is `decimal(18,2)`; totals are server-computed, never trusted from the client.
- Errors return RFC 7807 `ProblemDetails`. No stack traces leaked to clients.
- Conventional commits, imperative mood. No `wip` / `fix typo` noise.
- Every commit builds; from the testing PR onward, `dotnet test` is green.

## Living docs

Keep [`AI_USAGE.md`](AI_USAGE.md) current as you work — note what AI generated vs.
what was reviewed and corrected, per PR.
