# AI Usage

Honest disclosure of how AI was used to build this project. This is a **living
document** — updated as the work progresses, not written at the end.

## Tools used

- **Claude Code (Opus)** — used as a pair-programmer, directed by me.

## Division of responsibility

**I decided (the judgment — what this assessment actually grades):**
- Scope boundaries: what to build impeccably vs. deliberately leave out.
- Domain model: invoice + line items, server-computed money totals.
- Multi-tenancy strategy: shared DB + `TenantId` + EF global query filter.
- Status lifecycle and which transitions are legal.
- API shape, error contract, and the "no over-engineering" guardrails.
- The PR / commit plan and how the work is sequenced.

**AI generated (under my direction):**
- Boilerplate and scaffolding (solution, project files, hygiene config).
- First drafts of EF configurations, DTOs, and test scaffolding.
- First-draft documentation, which I reviewed and edited.

**I reviewed / corrected:**
- _(Filled in per PR as the build progresses.)_

**What AI got wrong:**
- _(Captured honestly as it happens — this transparency is part of the grade.)_

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
