# Code Review Follow-up

Second review pass after fixing findings #1, #2, #4, #5, #6, and #7 from
[CODE-REVIEW.md](CODE-REVIEW.md). Covers the remaining open items plus
observations on the code added during the fixes.

Date: 2026 (branch `develop`)

---

## Status of original findings

| # | Finding | Status |
|---|---------|--------|
| 1 | `finally` re-enable loop aborts early / masks original exception | ✅ Fixed (EFCore + EF6, tested) |
| 2 | Unbounded static mapping cache keyed by `IModel` | ✅ Fixed (`ConditionalWeakTable`, tested) |
| 3 | Sync-over-async wrappers | ⚠️ Open by choice (see below) |
| 4 | Zombie `SqlTransaction` during constraint re-enable | ✅ Fixed (shared `SqlTransactionHelper.IsZombied`, tested) |
| 5 | Reflection-heavy dynamic dispatch per call | ✅ Fixed (cached delegate dispatch, tested) |
| 6 | `Entities.First().GetType()` decides mapping for whole batch | ✅ Fixed (per-type-group sort/statistics, tested) |
| 7 | Unguarded `TimeSpan` → `CommandTimeout` conversion | ✅ Fixed (`ToCommandTimeoutSeconds`, all 4 sites, tested) |

---

## Remaining items

### #3 — Sync-over-async wrappers (decision pending)

All sync APIs still block with `.ConfigureAwait(false).GetAwaiter().GetResult()`
(e.g. `DbContextExtensions.cs` `BulkSelect`, `BulkSelectExisting`, `BulkInsertAll`).
`ConfigureAwait(false)` on the outer task does not protect against deadlocks on a
`SynchronizationContext` (WinForms/WPF/classic ASP.NET — relevant for the
.NET Framework 4.8 target).

Tradeoffs of the `Task.Run(...)` mitigation, discussed previously:

- ✅ Eliminates the `SynchronizationContext` deadlock completely.
- ❌ Extra thread-pool hop per call.
- ❌ Loses ambient `AsyncLocal` state (would break `TempTableTracker`-style scopes
  if callers rely on them across the sync API).
- ❌ Changes exception stack traces.

**Decision needed:** accept the documented behavior (current state) or switch the
sync wrappers to `Task.Run`.

### Minor / polish (all still open)

1. **`DBCC FREEPROCCACHE` string literal** —
   `Tanneryd.BulkOperations.EFCore/DbContextExtensions.cs` (~line 92) and
   `Tanneryd.BulkOperations.EF6/DbContextExtensions.cs` (~line 77):
   the `$@` prefixes are unnecessary; use a plain string literal. Trivial.

2. **`ObjectListDataReader.GetChars`** —
   `Tanneryd.BulkOperations.Common/Sql/ObjectListDataReader.cs` (~line 134):
   `((string)GetValue(ordinal)).ToCharArray()` allocates the entire string as a
   char array on every chunked read — O(n) per chunk for large strings.
   Use `string.CopyTo` directly into the caller's buffer. Cheap fix, real win
   for large string columns.

3. **`ArrayList` in the insert path** —
   `DbContextExtensions.Insert.cs` in both projects
   (EFCore ~lines 195, 818, 1017–1019; EF6 ~lines 185, 1001):
   non-generic `ArrayList` plus `dynamic` iteration boxes and defeats the JIT;
   `List<object>` is a drop-in replacement.

4. **Discriminator temp column hard-coded to `nvarchar(128)`** —
   `GetDiscriminatorExtraColumns` in `DbContextExtensions.Infrastructure.cs`
   (EFCore ~line 273, mirrored in EF6): string discriminator values longer than
   128 characters would silently truncate in the temp table, causing missed
   MERGE matches. Read the store type from the discriminator column metadata,
   or fall back to `nvarchar(max)`.
   **This is the only remaining item with a correctness dimension.**

5. **Dead `IF OBJECT_ID(...) DROP TABLE` guard in `CreateTempTableAsync`** —
   `DbContextExtensions.Infrastructure.cs` (EFCore ~line 245, EF6 ~line 205):
   the temp table name is a fresh GUID each time, so the guard never fires;
   one redundant statement per operation.

6. **Non-atomic `Created++` / `Dropped++`** —
   `TempTableTracker`, `SqlResourceTracker`, `SqlTransactionTracker`
   (`Tanneryd.BulkOperations.Common/Sql/`): increments race in parallel
   branches. Fine for the documented test-only usage; `Interlocked.Increment`
   would make them robust under parallel bulk operations.

---

## Observations on code added during the fixes (no action required)

- **#6 fix:** `TimeElapsedDuringSorting` now includes the clustered-index catalog
  query per type group — correct semantics, slightly different accounting than
  before for single-type batches (negligible).
- **#5 fix:** the EFCore invoker uses a `static` lambda in `GetOrAdd`; the EF6
  version does not (C# 7.3 on net48 has no static lambdas) — intentional
  divergence, no functional difference.
- **#7 fix:** all four `TimeSpan` conversion sites route through
  `SqlCommandFactory.ToCommandTimeoutSeconds`; no raw casts remain in
  production code.

---

## Recommended priorities

1. **Polish item 4** — discriminator `nvarchar(128)` truncation
   (correctness risk, testable).
2. **Polish items 2 and 3** — `GetChars` and `ArrayList` (easy perf wins).
3. **Polish items 1, 5, 6** — opportunistic cleanup, near-zero risk.
4. **#3** — make a deliberate decision: keep documented blocking behavior or
   switch to `Task.Run`.
