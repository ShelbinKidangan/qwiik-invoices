# Solution Notes

Engineering notes for the Qwiik Invoices module — a small, multi-tenant invoice
management API (5 endpoints, one bounded context). The intent throughout is a
well-modelled core over volume: a reviewer should understand the whole thing quickly.

---

## 1. How to run the project

### Docker Compose (one command)

```bash
docker compose up --build
```

Brings up SQL Server 2022, waits for its healthcheck, then builds and starts the API.
The API applies its EF migration on startup, so the schema is ready with no manual step.

- Interactive API reference (Scalar): **http://localhost:8080/scalar**
- Health: `GET /health` (liveness), `GET /health/ready` (readiness — includes the DB check)
- Quick smoke test: `GET http://localhost:8080/health/ready` should return `200` (API up +
  DB check passed) — it's block `### 0` in [`requests.http`](requests.http).

### Local dotnet (LocalDB)

The default connection string targets SQL Server LocalDB:

```bash
dotnet run --project src/Qwiik.Invoices.Api
# then apply the schema if the database does not exist yet:
dotnet ef database update --project src/Qwiik.Invoices.Api
```

In `Development` the app also migrates on startup, so `dotnet run` against an empty
database self-heals. Scalar is served at `/scalar` (the app root redirects there).

### Running the tests

Integration tests exercise a **real SQL Server** (the same behaviour a fake provider
cannot prove — global query filters, `rowversion` concurrency, unique-index retries).
Provide one of:

- **Docker running** — the suite starts a throwaway SQL Server via Testcontainers
  (default, nothing to configure), or
- **`QWIIK_TEST_SQL`** — set it to a LocalDB (or any SQL Server) connection string and
  the suite uses that directly. Point it at a **separate test database** — the suite
  applies the real migration and deletes all rows between tests.

```bash
# Testcontainers (Docker):
dotnet test

# LocalDB fallback:
QWIIK_TEST_SQL="Server=(localdb)\MSSQLLocalDB;Database=QwiikInvoices_Test;Trusted_Connection=True;MultipleActiveResultSets=true" dotnet test
```

### Trying the endpoints

- [`requests.http`](requests.http) — a runnable set covering all 5 endpoints plus a
  missing-tenant example. Run the blocks top to bottom (create captures the id and
  rowVersion the later blocks reuse).
- The **/scalar** UI documents every endpoint and its schemas, and can send requests
  interactively.

---

## 2. Assumptions

- **Single currency per invoice.** `Currency` is a stored 3-letter ISO code, echoed
  back but never converted — no FX or multi-currency arithmetic.
- **Tenant supplied via `X-Tenant-Id`.** A deliberate stub for a validated identity
  claim. In production the tenant id would come from a trusted JWT claim, not a
  client-supplied header.
- **Customer is denormalized.** Name and email are captured as fields on the invoice;
  there is no separate `Customer` entity or customer directory.
- **`Overdue` is set by explicit transition.** Nothing derives it automatically; a
  production system would flip `Sent → Overdue` from a scheduled job when `DueDate`
  passes. Here it is a manual status change.
- **`TaxRate` is a whole-number percent** (e.g. `10` = 10%). Line tax is
  `Quantity × UnitPrice × TaxRate / 100`.

---

## 3. Architecture overview

**Layered-lite inside one API project**, plus one test project — the whole module is
small enough that more structure would be ceremony, not clarity.

| Folder | Responsibility |
|--------|----------------|
| `Domain/` | Entities and business rules — `Invoice` aggregate, line items, status enum. |
| `Infrastructure/` | `DbContext`, EF configurations, migrations. |
| `Features/Invoices/` | Controller, thin service, DTOs, validation. |
| `Common/` | Cross-cutting: tenant context/middleware, paging, error handling. |

- **EF Core's `DbContext` *is* the unit of work** — used directly, with no repository
  or generic-repository wrapper over it. `SaveChanges` is the transaction boundary.
- **Business rules live in the domain entities**, not in the controller or service. The
  service is thin orchestration; the `Invoice` aggregate owns totals and transitions.

**Why not Clean Architecture (4+ projects) or MediatR/CQRS?** For a 5-endpoint module
with one aggregate, those patterns add project boundaries, indirection, and handler
plumbing without buying separation that a single well-factored project doesn't already
have. Read and write models are the same shape; there is no independent scaling or
complex dispatch to justify CQRS. The layered-lite split keeps the seams visible while
staying navigable in one project.

---

## 4. Domain model

The **`Invoice` aggregate root** owns its line items and is the only place money and
status are decided.

- **Owns line items.** `InvoiceLineItem`s are held in a private collection and exposed
  read-only; they are created and validated only through the aggregate.
- **Status lifecycle** — a strict state machine enforced by `ChangeStatus`:

  ```
  Draft   → Sent | Cancelled
  Sent    → Paid | Overdue | Cancelled
  Overdue → Paid
  Paid, Cancelled → (terminal)
  ```

  Any other transition throws `DomainException`.
- **Server-computed money.** `Subtotal`, `TaxTotal`, and `Total` are recomputed from the
  line items inside the aggregate and never accepted from outside — a client cannot
  supply or tamper with a total. Each line's net and tax are rounded to 2dp first, then
  summed, so the totals always reconcile with the sum of the displayed line totals.
- **Invariants enforced in the constructor.** Required customer/currency fields, a valid
  3-letter currency, `DueDate ≥ IssueDate`, and at least one line item are all checked
  on construction, so an `Invoice` cannot exist in an invalid state. Line-item
  invariants (non-empty description, quantity > 0, non-negative price/tax) are enforced
  in the `InvoiceLineItem` constructor.

---

## 5. Database design

- **SQL Server**, schema created and evolved through EF Core migrations
  (`InitialCreate`). The committed idempotent [`db/script.sql`](db/script.sql) is the
  DBA-friendly equivalent.
- **Money is `decimal(18,2)`** on every monetary column (subtotal, tax, total, line
  quantity/price/rate) — no floating point for money.
- **Concurrency via `rowversion`.** `Invoice.RowVersion` maps to a SQL `rowversion`
  column and is EF's concurrency token; a stale token on write raises a
  `DbUpdateConcurrencyException` (surfaced as `409`).
- **Cascade delete** from invoice to line items: the line item has no explicit
  back-reference, so EF introduces a shadow `InvoiceId` FK, marked required with
  `OnDelete(Cascade)` — deleting an invoice removes its lines.
- **Private parameterless constructor for EF.** The aggregate exposes only a validating
  public constructor (which EF cannot bind, because it takes line items as a
  collection). A private ctor lets EF materialize rows while the public ctor stays the
  only path application code has to build an invoice, so all invariants hold.

---

## 6. API design

Base route: **`/api/v1/invoices`**. All responses are JSON.

| Method & path | Purpose |
|---------------|---------|
| `POST /api/v1/invoices` | Create an invoice (server derives number, status, totals). |
| `GET /api/v1/invoices/{id}` | Fetch one invoice with its line items. |
| `GET /api/v1/invoices` | List — paged, filterable by status and issue-date range. |
| `PATCH /api/v1/invoices/{id}/status` | Change status (optimistic-concurrency checked). |
| `GET /api/v1/invoices/summary` | Per-tenant roll-up: counts and totals by status. |

- **`PATCH` for status** expresses partial intent — the request changes one field
  (status) against a concurrency token, not the whole resource.
- **Error contract is RFC 7807 `ProblemDetails`** for every failure (validation, not
  found, conflict, missing tenant). A `traceId` extension correlates a client error to
  the server logs; stack traces are never leaked.
- **`PagedResult<T>`** carries `Items`, `Page`, `PageSize`, `TotalCount`, and a derived
  `TotalPages`, so a client can render paging without guessing.
- **DTOs carry no server-owned fields.** Create/update requests contain no `TenantId`,
  `Id`, `Status`, `InvoiceNumber`, or totals — all are server-derived, so none can be
  forged via mass-assignment.

---

## 7. Validation

- **FluentValidation at the boundary.** `CreateInvoiceValidator` fast-fails the request
  shape (required fields, lengths, currency length, `DueDate ≥ IssueDate`, per-line-item
  rules) and returns a clean field-level `400` before any domain object is built.
- **The domain constructor is the last guard.** The validator deliberately *mirrors* the
  domain invariants rather than replacing them — even if a request reaches the aggregate
  unvalidated (e.g. a future caller), construction still enforces every rule.
- **Email format is validated only at the validation layer.** The domain treats email as
  a required non-empty string; RFC-shape checking lives in FluentValidation, not in the
  entity.

---

## 8. Tenant isolation

Non-negotiable, enforced by construction rather than by discipline.

- **Shared database, `TenantId` on every tenant-owned row.**
- **EF Core global query filter.** `Invoice` has
  `HasQueryFilter(i => i.TenantId == _tenant.TenantId)`. The filter captures the
  *injected* `ITenantContext`, not a snapshot value, so it resolves the current request's
  tenant every time a query is compiled — no read can leak across tenants.
- **`TenantId` is stamped on write from the tenant context**, in `SaveChanges`, never
  bound from a DTO. Request models don't even contain the field, so mass-assignment is
  impossible; the placeholder `Guid.Empty` passed into the aggregate is overwritten by
  the stamp.
- **Proven by integration tests on real SQL** — both sides: a tenant cannot *read*
  another tenant's invoices (query filter), and a write is stamped with the caller's
  tenant regardless of input (write path).

---

## 9. Indexing and performance

- **Composite indexes lead with `TenantId`**, because every query is tenant-scoped:
  - `(TenantId, Status)` — status filters and the summary group.
  - `(TenantId, IssueDate)` — the default list ordering and date-range filter.
  - `(TenantId, InvoiceNumber)` **unique** — enforces per-tenant number uniqueness and
    backs the create-time collision retry.
- **Reads use `AsNoTracking()` and project to DTOs.** The list projection omits line
  items to avoid over-fetch; only the columns the row needs are selected.
- **Summary is a single `GROUP BY Status`** round trip returning per-status count and
  total; the roll-up (outstanding = Sent + Overdue, paid, overdue count/amount) is
  composed in memory from those few rows rather than pushed into SQL `CASE` expressions.
- **Paging** is offset-based (`Skip`/`Take`) with a stable `IssueDate desc, Id` order and
  a separate `COUNT` for `TotalCount` — the honest cost of offset paging at this scale.
- **Scale path:** keyset (seek) pagination if lists grow large, and read replicas /
  `AsSplitQuery` for read-heavy reporting — unnecessary at this module's size.

---

## 10. Testing

Two tiers, each covering what it is best placed to prove — no coverage theater.

- **Unit tests (fast, no database)** — the domain rules in isolation: the status
  transition matrix (legal and illegal moves), money totals and rounding, and
  constructor invariants for `Invoice` and `InvoiceLineItem`, plus `PageQuery`
  normalization.
- **Integration tests (real SQL Server)** — run against Testcontainers by default, with a
  `QWIIK_TEST_SQL` LocalDB fallback, applying the **real EF migration** (not
  `EnsureCreated`) so the suite exercises the production schema. These cover the things a
  fake provider cannot: **tenant isolation** through the global query filter (read and
  write), **optimistic concurrency** (stale `RowVersion` → `409`) against the actual SQL
  `rowversion`, the **invoice-number collision retry** against the unique index,
  end-to-end endpoint behaviour (validation `400`s, `404`s, listing/paging/filter, status
  changes), and the missing-tenant `400`.
- **Run in CI on every push/PR to `main`** — GitHub Actions builds in Release and runs the
  whole suite, provisioning SQL Server via Testcontainers on the runner's Docker daemon
  (see §11), so a regression cannot merge green.
- **Deliberately not covered:** exhaustive controller permutations already guaranteed by
  the type system or by the domain unit tests, and load/performance testing — out of
  scope for a module of this size.

---

## 11. Azure deployment and monitoring

A pragmatic, cost-aware path — not an enterprise landing zone.

- **Compute — Azure Container Apps** (the image already exists) for scale-to-zero and
  simple revisions; **App Service for Containers** is an equally good fit if the org
  standardizes on it. Either runs the published container directly.
- **Database — Azure SQL Database, serverless tier**, which auto-pauses under no load —
  a good match for a bursty module and cheaper than a provisioned tier while small.
- **Secrets — Azure Key Vault + managed identity.** The app authenticates to SQL and Key
  Vault with its managed identity; no connection-string secrets in config or images.
- **Configuration — 12-factor via environment / Azure App Configuration.** Everything
  environment-specific (connection strings, log levels) comes from configuration, exactly
  as `ConnectionStrings__DefaultConnection` does locally.
- **CI is in place — GitHub Actions** (`.github/workflows/ci.yml`) restores, builds
  (Release), and runs the full test suite on every push and PR to `main`; the integration
  tests provision a real SQL Server via Testcontainers on the runner's Docker daemon.
  **CD is the planned extension** — build and push the image, deploy to a **staging
  slot**, then swap to production for **zero-downtime** releases and instant **rollback
  via swap-back**.
- **Migrations as a release gate**, not auto-run in prod — a dedicated pipeline step
  applies the idempotent `db/script.sql` (or `dotnet ef database update`) under change
  control before the swap. The app only auto-migrates outside Production.
- **Monitoring — Application Insights + Log Analytics.** Serilog already emits structured
  JSON with correlation and tenant ids; ship it to Log Analytics and alert on **5xx
  rate, request latency, and SQL DTU/utilization**. Readiness (`/health/ready`) drives
  the platform's health probe.
- **Scale — horizontal autoscale** on CPU/HTTP concurrency (Container Apps replicas). The
  app is stateless, so scaling out is safe.
- **Cost notes:** serverless SQL + scale-to-zero compute keep idle cost near the floor;
  the main levers under load are SQL tier and replica count.

---

## 12. Security considerations

- **No SQL injection** — all data access is through EF Core with parameterized queries;
  there is no string-concatenated SQL.
- **No mass-assignment** — `TenantId`, `Id`, `Status`, `InvoiceNumber`, and totals are
  server-owned and absent from request DTOs, so a client cannot set them.
- **No information leakage** — errors are RFC 7807 `ProblemDetails`; stack traces and
  internal exception detail never reach the client.
- **Transport — HTTPS redirection** is enabled; HSTS would be added at the edge/host in
  production.
- **Secrets** come from configuration / Key Vault, never source or images (see §11).
- **Least-privilege SQL login** — the app's database principal needs only CRUD on its own
  tables; schema changes run as a separate migration identity, not the app.
- **Input limits and validation** — every string field has a bound `MaximumLength`, page
  size is clamped server-side (`MaxPageSize = 100`), and validation runs before any work.
- **Dependency-vulnerability awareness** — `Microsoft.OpenApi` is pinned to a patched
  `2.10.0` because the OpenAPI package pulls a `2.0.0` transitively that carries advisory
  GHSA-v5pm-xwqc-g5wc (NU1903); `dotnet list package --vulnerable --include-transitive`
  is clean.

---

## 13. Known limitations

Named honestly rather than hidden.

- **`X-Tenant-Id` is a stub.** Production would resolve the tenant from a validated JWT
  claim, not a client-supplied header.
- **No real auth or user management**, and no payments, PDF rendering, email, webhooks,
  or multi-currency FX.
- **No `Customer` entity** — customer name/email are denormalized onto the invoice; there
  is no customer record to update or de-duplicate against.
- **No soft-delete or audit-history tables** — rows are updated in place; there is no
  change log beyond `CreatedAtUtc`/`UpdatedAtUtc`.
- **Invoice number is `MAX+1` with a unique-index retry.** The number is
  `INV-{year}-{seq:D5}`, computed as `1 + MAX(seq)` for the tenant/year. Two concurrent
  creates can compute the same number; the unique `(TenantId, InvoiceNumber)` index
  rejects the loser and the service recomputes and retries, **bounded at 3 attempts**. If
  those are exhausted, the create currently surfaces a `500` rather than a mapped `409`.
  A per-tenant sequence table would be the race-free answer but adds a table, migration,
  and locking this scale doesn't warrant.
- **`RowVersion` is mandatory to change status.** Optimistic concurrency is required on
  that endpoint — the client must send the base64 `RowVersion` it last read, in the
  request body (not an `If-Match`/ETag header); a missing or malformed token is a `400`,
  a stale one a `409`.
- **`Overdue` is a manual transition**, not a scheduled or derived state (see §2).
- **No rate limiting or API-versioning infrastructure** beyond the `/v1` path segment.

---

## 14. What I would improve with more time

- **Derive `Overdue` via a background job** that flips `Sent → Overdue` when `DueDate`
  passes, instead of relying on a manual transition.
- **Introduce a `Customer` entity** so customers are first-class (dedupe, update once,
  richer reporting) rather than denormalized per invoice.
- **Move concurrency to `ETag`/`If-Match` headers** instead of a body `RowVersion` — the
  more RESTful expression of optimistic concurrency.
- **Map exhausted invoice-number retries to `409`** (a clean, retryable conflict) rather
  than letting them surface as a `500`.
- **Add rate limiting** at the API edge to protect against abusive callers.
- **Richer summary with caching** — more roll-up dimensions (e.g. by period/customer)
  with short-TTL caching, since the summary is read-often and changes slowly.
- **Broaden integration coverage** — more endpoint permutations and edge cases now that
  the real-SQL harness makes them cheap to add.
