# Solution Notes

Deliberate scope boundaries and trade-offs. Anything intentionally left out or
simplified is named here rather than silently skipped or half-built.

## Out of scope (by design)

- No real auth server — the tenant is resolved from the `X-Tenant-Id` header as a
  stand-in for a validated JWT claim (see Stage #3).
- No payments, PDF rendering, email, webhooks, or multi-currency FX. `Currency` is
  stored and echoed but never converted.
- No Clean Architecture (4+ projects), CQRS, MediatR, microservices, repository/
  generic-repository over EF, or AutoMapper. One API project + one test project.

## Invoice API (Stage #4)

- **InvoiceNumber generation** — `INV-{year}-{seq:D5}`, sequence is `1 + MAX(seq)`
  over the tenant's invoices for that year. Two concurrent creates can compute the
  same number; the unique `(TenantId, InvoiceNumber)` index rejects the loser and the
  service recomputes and retries (bounded). A dedicated per-tenant counter/sequence
  table would be the race-free production answer, but adds a table, migration, and
  locking that this module's scale does not warrant.
- **Optimistic concurrency on status change** — the client's `RowVersion` travels in
  the request body as base64, not as an `If-Match`/ETag header. Body is simpler and
  explicit here; header-based ETags would be the more RESTful choice at larger scope.
- **Error handling** — domain, not-found, and concurrency errors map to RFC 7807
  `ProblemDetails` locally in the controller for now. Centralising this into a single
  exception-handling middleware is deferred to Stage #5.
- **List paging** — `TotalCount` is a second `COUNT` round trip alongside the page
  query. This is the standard, honest cost of offset paging; keyset paging is
  unnecessary at this scale.
- **Summary** — a single `GROUP BY Status` query returns per-status count and total;
  the roll-up (outstanding = Sent + Overdue, paid, overdue count/amount) is composed
  in memory from those few rows rather than pushed into SQL `CASE` expressions.

## Run tooling (Stage #7)

- **One-command run** — `docker compose up --build` starts SQL Server 2022, waits on
  its healthcheck, then builds and starts the API (multi-stage Dockerfile). Scalar is
  at `http://localhost:8080/scalar`; `requests.http` exercises all 5 endpoints.
- **Migration on startup** — the API calls `Database.Migrate()` on boot, guarded to
  skip `Production`, so the compose flow comes up with a ready schema and no manual
  step. The trade-off: applying migrations from the web process is convenient for
  dev/demo but wrong for production, where concurrent instances would race and a failed
  migration should block the deploy, not a running app. **Production strategy:** apply
  migrations as a separate, auditable gate — a CI/CD step, or hand the committed
  idempotent `db/script.sql` (from `dotnet ef migrations script --idempotent`) to a DBA
  to run under change control. The app process never migrates in prod.

## Deferred to later stages

- Centralised exception→ProblemDetails middleware — Stage #5.
- Tests (create/get/list/status/summary + tenant isolation) — Stage #6.
