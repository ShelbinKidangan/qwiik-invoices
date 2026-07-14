# AI Usage

An honest record of how AI was used to build this project. This is a **living
document** — updated as the work progresses, not written at the end.

## Method — how this was built

Built in **7 sequential stages**, each a self-contained slice that compiles
green (and from the testing stage on, passes tests): Foundation → Domain &
persistence → Multi-tenancy → Invoice API → Production hardening → Testing →
Docs & CI. Every change landed as its own small, reviewable commit on `main`.

The AI-assisted workflow, kept deliberately disciplined:

- **Scoped commit.** Each change was scoped small enough to review as a
  single diff — no large multi-feature generations.
- **I reviewed every diff before it landed.** AI drafted; I accepted, corrected,
  or rejected. Nothing was committed unread.
- **Guardrails set before generating.** `CLAUDE.md` fixed the conventions and the
  "no over-engineering" boundaries up front, so generated code matched the
  house style instead of being reshaped after.
- **Design decided by me, plumbing by AI.** Domain invariants, tenancy strategy,
  status lifecycle, and API contract were my calls; scaffolding, EF config, and
  test harness were AI first-drafts under that direction.
- **Verified behaviour, not just the build.** Tenant isolation and optimistic
  concurrency were proven against real SQL Server, not asserted.
- **Trade-offs and AI mistakes disclosed, not hidden** — see below.

## Tools used

- **Claude Code (Opus)** — used as a pair-programmer, directed by me.

## Division of responsibility

**I decided (the design and judgment):**
- Scope boundaries: what to build carefully vs. deliberately leave out.
- Domain model: invoice + line items, server-computed money totals.
- Multi-tenancy strategy: shared DB + `TenantId` + EF global query filter.
- Status lifecycle and which transitions are legal.
- API shape, error contract, and the "no over-engineering" guardrails.
- The stage / commit sequencing.

**AI generated (under my direction):**
- Boilerplate and scaffolding (solution, project files, hygiene config).
- First drafts of EF configurations, DTOs, and test scaffolding.
- First-draft documentation, which I reviewed and edited.

**I reviewed / corrected:**
- _(Filled in per stage as the build progresses.)_

**What AI got wrong:**
- **Rounding drift between line totals and the invoice total.** AI's first draft of the
  money math summed the raw line amounts and rounded once at the end (`Total`), while
  each `LineTotal` rounded per line — so the displayed line column could sum to a cent
  more/less than `Total`. Defensible, but wrong for an invoice, where the line column
  must reconcile. Corrected to round each line's net and tax to 2dp first, then sum, so
  `Subtotal + TaxTotal = Total = Σ LineTotal` by construction. (Domain unit tests still
  pass unchanged — the asserted totals were unaffected; only the reconciliation was.)
- **Inconsistent table naming.** AI mapped `InvoiceLineItem` without a `DbSet` (correct —
  it is an aggregate-internal entity, not a root), but that left EF naming the table
  after the singular type while `Invoices` was plural. Fixed with an explicit
  `ToTable("InvoiceLineItems")` and a rename migration. Both were first written up
  honestly as known limitations, then fixed rather than left documented.

## Log by Stage

### Stage #1 — Foundation & scaffolding
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

### Stage #2 — Domain & persistence
- The `Invoice` aggregate, its `InvoiceLineItem`s, and the status enum were modelled
  here, with the business rules (totals, transitions, invariants) living in the entities.
- AI generated: the EF Core `IEntityTypeConfiguration` classes, the `DbContext`, and the
  `InitialCreate` migration scaffolding.
- I decided the domain shape, the status transition matrix, and the indexing strategy —
  the composite indexes lead with `TenantId` because every query is tenant-scoped.
- **AI reviewed / corrected:** design-time model creation failed because the `Invoice`
  aggregate exposes only a validating constructor that takes its line items as a
  navigation, which EF cannot bind. Added a private parameterless constructor for EF to
  materialize the entity; the public constructor stays the only way application code can
  build an invoice, so all domain invariants remain enforced.

### Stage #3 — Multi-tenancy
- I decided the isolation strategy: shared database + `TenantId` on every tenant-owned
  row + an EF Core global query filter so no query can leak across tenants by
  construction. The `X-Tenant-Id` header is a deliberate stub for local/dev — in
  production the tenant comes from a validated JWT claim, not a client header.
- AI wrote: the `TenantResolutionMiddleware` (header parse/validate, 400
  `ProblemDetails` on missing/invalid, anonymous pass-through for `/`, `/scalar`,
  `/openapi`, `/health`), the scoped `ITenantContext`/`TenantContext`, and the
  `DbContext` wiring — the captured-tenant query filter plus stamping `TenantId` from
  context on insert (never bound from a request DTO, so no mass-assignment).

### Stage #4 — Invoice API (the 5 endpoints)
- I decided the API shape (`/api/v1/invoices`), the DTO boundaries (no `TenantId`/`Id`/
  `Status`/money on any request), the thin-service split, and the two trade-offs now in
  `SOLUTION_NOTES.md` (InvoiceNumber via MAX+1 with unique-index retry; RowVersion in the
  request body rather than an ETag header).
- AI wrote: the `InvoicesController` (create/get/list/status/summary), the thin
  `InvoiceService` (orchestration only — all rules stay in the `Invoice` aggregate),
  hand-written DTO mapping, the `CreateInvoiceValidator` (FluentValidation), and the
  `PageQuery`/`PagedResult<T>` primitives.
- **Design choices under my direction:** reads use `AsNoTracking()` and project to DTOs
  (the list row omits line items to avoid over-fetch); the summary is a single
  `GROUP BY Status` query rolled up in memory; `[ApiController]`'s automatic model-state
  400 is suppressed so FluentValidation owns request validation.
- **AI reviewed / corrected:** first draft used `AddToModelState`, which lives in the
  `FluentValidation.AspNetCore` package; kept the dependency to core `FluentValidation`
  and mapped validation failures into `ModelState` by hand instead.

### Stage #5 — Production hardening
- I decided the cross-cutting concerns: one RFC 7807 error contract, per-request
  correlated structured logs, and liveness/readiness health probes for the platform.
- AI wrote: the `GlobalExceptionHandler` mapping unhandled exceptions to `ProblemDetails`
  centrally (no controller leaks a stack trace), the `CorrelationIdMiddleware` + Serilog
  structured logging that stamps `CorrelationId` (and `TenantId`) on every log line and
  echoes `X-Correlation-Id` back, and the `/health` (liveness) / `/health/ready`
  (readiness — includes the DB check) endpoints.

### Stage #6 — Testing
- I decided the test strategy and the provider choice: integration tests run against a
  **real SQL Server** — Testcontainers by default with a **LocalDB fallback** via the
  `QWIIK_TEST_SQL` env var — applying the **real EF migration** (not `EnsureCreated`), so
  the suite exercises the production schema. On that harness I chose to prove the things a
  fake provider can't: **tenant isolation** through the global query filter and
  **optimistic concurrency** and **invoice-number collision retry** against the actual
  SQL `rowversion` and unique index.
- AI wrote: the integration harness (`SqlServerFixture`, `InvoiceApiFactory`,
  `IntegrationTestBase`, the shared collection) and the test cases across the domain and
  integration suites — including the stale-rowversion 409 case and the concurrent
  same-tenant create race that guards the line-item-reuse fix.

### Stage #7 — Docs, CI & final hardening
- AI drafted the living docs and delivery artifacts under my review: `SOLUTION_NOTES.md`
  (architecture, trade-offs, Azure plan), the `README` quickstart, `requests.http`, and
  the `docker compose` / `Dockerfile` / idempotent `db/script.sql` one-command run.
- **CI:** a GitHub Actions workflow (`.github/workflows/ci.yml`) restores, builds
  (Release), and runs the full test suite on every push and PR to `main` — the
  integration tests spin up a real SQL Server via Testcontainers on the runner's Docker
  daemon, so "compiles green and passes tests" is enforced, not just claimed.
- I reviewed the two trade-offs originally logged as known limitations and decided to fix
  rather than keep documenting them (see **What AI got wrong** above).
- **Totals reconciliation:** changed the money math to round each line's net and tax to
  2dp before summing, so `Subtotal + TaxTotal = Total = Σ LineTotal` by construction. The
  existing totals tests passed unchanged (their asserted values were unaffected) — which
  is exactly why the drift slipped through, so AI added a dedicated regression test
  (`RecalculateTotals_WithFractionalCentRounding_TotalEqualsSumOfLineTotals`) asserting
  the reconciliation invariant on an input that previously drifted a cent.
- **Table naming:** added `ToTable("InvoiceLineItems")` plus a rename migration so the
  table is plural, consistent with `Invoices`. The rename is already exercised by the
  integration suite, which applies the real migration against real SQL — no extra test
  warranted.
- I updated `SOLUTION_NOTES.md` to drop the two resolved limitations and note the
  reconciliation guarantee in the domain-model section.
