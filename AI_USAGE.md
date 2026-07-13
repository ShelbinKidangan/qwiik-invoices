# AI Usage

An honest record of how AI was used to build this project. This is a **living
document** — updated as the work progresses, not written at the end.

## Tools used

- **Claude Code (Opus)** — used as a pair-programmer, directed by me.

## Division of responsibility

**I decided (the design and judgment):**
- Scope boundaries: what to build carefully vs. deliberately leave out.
- Domain model: invoice + line items, server-computed money totals.
- Multi-tenancy strategy: shared DB + `TenantId` + EF global query filter.
- Status lifecycle and which transitions are legal.
- API shape, error contract, and the "no over-engineering" guardrails.
- The PR / commit sequencing.

**AI generated (under my direction):**
- Boilerplate and scaffolding (solution, project files, hygiene config).
- First drafts of EF configurations, DTOs, and test scaffolding.
- First-draft documentation, which I reviewed and edited.

**I reviewed / corrected:**
- _(Filled in per PR as the build progresses.)_

**What AI got wrong:**
- _(Captured honestly as it happens.)_

## Log by PR

### PR #1 — Foundation & scaffolding
- AI generated: `.gitignore`, `.editorconfig`, `LICENSE`, README/CLAUDE/AI_USAGE stubs,
  and the solution scaffold.
- I directed the structure (two projects, not a multi-project Clean Architecture layout)
  and the guardrails in `CLAUDE.md`.
- **Security:** AI flagged a transitive high-severity advisory (GHSA-v5pm-xwqc-g5wc /
  NU1903) — the webapi template's `Microsoft.AspNetCore.OpenApi` pulls
  `Microsoft.OpenApi 2.0.0`. I verified a patched release exists on the same major line
  and pinned `Microsoft.OpenApi 2.10.0` directly, keeping API compatibility.
  `dotnet list package --vulnerable --include-transitive` now reports clean.
- **API UI:** the .NET 10 template ships only the OpenAPI document (no UI). Added
  Scalar (`Scalar.AspNetCore`) as the interactive reference at `/scalar` and set it as
  the dev launch URL — the modern replacement for the Swashbuckle Swagger UI that
  Microsoft dropped from the templates.

### PR #2 — Persistence (EF Core)
- AI generated: the EF Core `IEntityTypeConfiguration` classes, the `DbContext`, and the
  `InitialCreate` migration scaffolding.
- I decided the domain shape, the status transition matrix, and the indexing strategy —
  the composite indexes lead with `TenantId` because every query is tenant-scoped.
- **AI reviewed / corrected:** design-time model creation failed because the `Invoice`
  aggregate exposes only a validating constructor that takes its line items as a
  navigation, which EF cannot bind. Added a private parameterless constructor for EF to
  materialize the entity; the public constructor stays the only way application code can
  build an invoice, so all domain invariants remain enforced.

### PR #3 — Multi-tenancy
- I decided the isolation strategy: shared database + `TenantId` on every tenant-owned
  row + an EF Core global query filter so no query can leak across tenants by
  construction. The `X-Tenant-Id` header is a deliberate stub for local/dev — in
  production the tenant comes from a validated JWT claim, not a client header.
- AI wrote: the `TenantResolutionMiddleware` (header parse/validate, 400
  `ProblemDetails` on missing/invalid, anonymous pass-through for `/`, `/scalar`,
  `/openapi`, `/health`), the scoped `ITenantContext`/`TenantContext`, and the
  `DbContext` wiring — the captured-tenant query filter plus stamping `TenantId` from
  context on insert (never bound from a request DTO, so no mass-assignment).
