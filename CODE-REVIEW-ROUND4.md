# Code Review — Round 4

**Date:** 2026 (post round-3 cleanup)
**Scope:** `Tanneryd.BulkOperations.EFCore` (net10.0), `Tanneryd.BulkOperations.EF6` (net48), shared `Tanneryd.BulkOperations.Common`
**Baseline:** All round-3 items (2, 3, 4, 6, 7 partial, 8 deferred) implemented; 322/322 tests passing.

---

## Summary

The codebase is in good shape. No functional or correctness issues were found in this
pass. The remaining items are packaging/polish tradeoffs, of which only two are
recommended before the next publish.

---

## What's healthy

- **Sync-over-async wrappers** — intentionally left as-is (documented decision).
  All wrappers consistently use `ConfigureAwait(false).GetAwaiter().GetResult()`,
  which is deadlock-safe for this pattern.
- **Async hygiene** — `ConfigureAwait(false)` coverage is complete across all three
  library projects; every `await` in library code is covered.
- **Exception handling** — only two broad catches remain, both justified:
  - The narrowed rollback catch in `DbContextExtensions.Update.cs` (skips zombied
	transactions, only swallows `InvalidOperationException` during rollback,
	rethrows via `throw;`).
  - Constraint re-enable aggregation in `DbContextExtensions.Infrastructure.cs`
	(collects failures and rethrows via `ExceptionDispatchInfo` /
	`AggregateException`; never silently swallows).
- **No code smells** — no `TODO`/`HACK`/`FIXME` markers, no `.Result`/`.Wait()`
  misuse, no debug leftovers (`Console.Write`, `ArrayList`) in library code.
- **Tests** — full suite green: 322/322 across EFCore and EF6.NET48.

---

## Findings

### 1. `GenerateDocumentationFile` not enabled — **Recommended**

The round-3 XML documentation added to the public model types is not emitted to the
NuGet packages because neither library csproj sets
`<GenerateDocumentationFile>true</GenerateDocumentationFile>`. NuGet consumers
therefore get no IntelliSense docs, making the documentation work invisible.

**Fix:** enable the property in both
`Tanneryd.BulkOperations.EFCore.csproj` and `Tanneryd.BulkOperations.EF6.csproj`.
This will surface CS1591 warnings for still-undocumented public members; either
suppress with `<NoWarn>$(NoWarn);1591</NoWarn>` or finish documenting the
remaining public surface.

### 2. Nullable reference types not enabled — Deferred

`<Nullable>enable</Nullable>` is not set. Enabling it is a large mechanical effort
across a mature codebase with limited payoff at this point. Acceptable to leave.

### 3. Analyzers not enabled — Deferred

No `AnalysisLevel` / `EnableNETAnalyzers` configuration. Low value until stricter
CI gates are desired. Acceptable to leave.

### 4. Stale `PackageReleaseNotes` — **Recommended**

Both csproj files still describe the async-API release ("Added async bulk operation
APIs with CancellationToken support…") rather than the 4.0.0 changes from the
recent review rounds (nullable-aware key join predicates, optimistic-concurrency
fixes, narrowed rollback handling, performance polish, XML docs).

**Fix:** refresh `PackageReleaseNotes` in both library csproj files before
publishing 4.0.0.

---

## Verdict

Ship-ready apart from packaging polish. Recommended before the next publish:

| # | Item | Effort |
|---|------|--------|
| 1 | Enable `GenerateDocumentationFile` (+ `NoWarn 1591` or finish docs) | Small |
| 4 | Refresh `PackageReleaseNotes` for 4.0.0 | Trivial |

Items 2 and 3 remain intentionally deferred. Sync-over-async wrappers remain
intentionally open per the earlier decision.
