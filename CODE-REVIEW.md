# Code Review Findings

Review of the EF Core bulk operations library (`Tanneryd.BulkOperations.EFCore` and
`Tanneryd.BulkOperations.Common`), covering the public API surface (`DbContextExtensions.*`),
the shared SQL helpers, and the insert pipeline.

Date: 2026 (branch `develop`)

---

## High-impact issues

### 1. `finally` re-enable loop can abort early and mask the original exception

`Tanneryd.BulkOperations.EFCore/DbContextExtensions.cs` (`BulkInsertAllAsync`, ~line 443):

```csharp
finally
{
	foreach (var tableName in response.TablesWithNoCheckConstraints)
		await ReenableCheckConstraintsAsync(ctx, tableName, request.Transaction).ConfigureAwait(false);
}
```

`ReenableCheckConstraintsAsync` deliberately **rethrows** when `WITH CHECK` fails. Inside this `finally`:

- If table #1 fails, tables #2..n are never re-enabled — defeating the very purpose of the `finally`.
- An exception thrown from `finally` **replaces** the original insert/cancellation exception,
  hiding the root cause.

**Suggestion:** catch per-table, collect failures, and after the loop throw an `AggregateException`
only if the `try` body completed successfully (or attach failures via `Exception.Data` / log them).
At minimum, `try/catch` each iteration so all tables get re-enabled.

### 2. Unbounded static cache keyed by `IModel`

`Tanneryd.BulkOperations.EFCore/DbContextExtensions.Infrastructure.cs` (`GetMappingExtractor`):
`_mappingExtractorsByModel` is a static `Dictionary<IModel, MappingsExtractor>` that grows forever.
For apps that build models dynamically (e.g., `DbContextOptions` per tenant, model recompilation in
tests), this is a memory leak — the `IModel` and everything the extractor captures are rooted
permanently. Also verify `MappingsExtractor(ctx)` doesn't retain the `DbContext` instance itself;
caching a per-context object under a per-model key would keep a disposed context alive and could
serve stale state.

**Suggestion:** use `ConditionalWeakTable<IModel, MappingsExtractor>` (drop `_mutex`; it has
thread-safe `GetValue`), and ensure the extractor only stores model metadata, not the context.

### 3. Sync-over-async wrappers

Every sync API blocks with `.ConfigureAwait(false).GetAwaiter().GetResult()`. The XML remark
acknowledges it, but note that `ConfigureAwait(false)` on the *outer* task does **not** protect
against deadlocks — it only affects the continuation of that one await. On a
`SynchronizationContext` (WinForms/WPF/classic ASP.NET, relevant since the solution also ships a
.NET Framework 4.8 target) any inner non-`ConfigureAwait(false)` await (including inside
EF Core/SqlClient) can still deadlock.

**Suggestion:** use `Task.Run(() => ...).GetAwaiter().GetResult()` in the sync wrappers to escape
the context, or document the constraint more strongly.

---

## Medium

### 4. `ReenableCheckConstraintsAsync` ignores ambient transaction semantics

If the caller's `SqlTransaction` was rolled back/aborted (typical after a cancelled bulk copy),
executing `ALTER TABLE` on that transaction will throw `InvalidOperationException`
(zombie transaction), which — combined with issue #1 — masks the original error.

**Suggestion:** detect a dead transaction (`transaction?.Connection == null`) and re-enable
outside it.

### 5. Reflection-heavy dynamic dispatch per call

`BulkSelectNotExistingByTypeAsync` (`DbContextExtensions.cs`, ~line 231) does `MakeGenericType`,
`GetMethods().Single(...)`, `MakeGenericMethod`, and `GetProperty("Result")` on every invocation.

**Suggestion:** cache the closed `MethodInfo`/property setters per `Type` in a
`ConcurrentDictionary<Type, ...>` — this path runs inside recursive inserts, so it's hot.

### 6. `Entities.First().GetType()` decides the mapping for the whole batch

`BulkInsertAllAsync` uses the first entity's type for `GetTableName`/clustered-index sorting even
though `DoBulkInsertAllAsync` later splits mixed-type batches. With mixed TPH types,
`SortUsingClusteredIndex` and `UpdateStatistics` only apply to the first type's table.

**Suggestion:** if mixed batches are supported (the splitting logic implies they are),
sorting/statistics should be applied per type group — or mixed batches should be rejected when
those options are set.

### 7. `TimeSpan` → `CommandTimeout` conversion is unguarded

`Tanneryd.BulkOperations.Common/Sql/SqlCommandFactory.cs`: `(int)timeout.TotalSeconds` —
`TimeSpan.Zero` silently means *infinite* timeout in ADO.NET, negative values throw later with an
unhelpful error, and `TimeSpan.MaxValue` overflows to a negative int.

**Suggestion:** validate and clamp — throw for negative values, and use
`checked` / `Math.Min(int.MaxValue, ...)` for large values.

---

## Minor / polish

- **`DeleteAllExecutionPlansFromCacheAsync`** (`DbContextExtensions.cs`):
  `$@"DBCC FREEPROCCACHE WITH NO_INFOMSGS"` — the interpolation/verbatim prefixes are unnecessary;
  use a plain string literal.
- **`ObjectListDataReader.GetChars`** (`Tanneryd.BulkOperations.Common/Sql/ObjectListDataReader.cs`)
  calls `((string)GetValue(ordinal)).ToCharArray()` on every invocation — for chunked reads of
  large strings this is O(n) per chunk. Use `string.CopyTo` into the buffer instead.
- **`ObjectListDataReader.GetSchemaTable`** omits `ColumnSize`, `NumericPrecision`, and `IsKey`
  columns; `SqlBulkCopy` doesn't need them, but if the reader is ever reused elsewhere, consider
  `SchemaTableColumn` constants for the column names rather than string literals.
- **`TempTableTracker.Scope`** (`Tanneryd.BulkOperations.Common/Sql/TempTableTracker.cs`):
  `Created++` / `Dropped++` are non-atomic, and while `AsyncLocal` flows into child tasks,
  increments in parallel branches race. Fine for tests as documented, but `Interlocked.Increment`
  on an `int` field would make it robust for parallel bulk ops.
- **Discriminator temp column typing** (`GetDiscriminatorExtraColumns`,
  `DbContextExtensions.Infrastructure.cs`): string discriminators are hard-coded to
  `nvarchar(128)`. EF discriminator values can exceed that (long class names in custom
  discriminators) and would silently truncate in the temp table, causing missed MERGE matches.
  Prefer reading the store type from `discriminator.Column` metadata, or use `nvarchar(max)`.
- **`CreateTempTableAsync`** (`DbContextExtensions.Infrastructure.cs`): the
  `IF OBJECT_ID(...) DROP TABLE` guard is dead code — the name is a fresh GUID each time;
  removing it saves a redundant statement per operation.
- **`validEntities` as `ArrayList`** (`DbContextExtensions.Insert.cs`, ~line 199): non-generic
  `ArrayList` plus `dynamic` iteration boxes and defeats the JIT; `List<object>` is a drop-in
  replacement. Similarly, `request.Entities.Cast<dynamic>().ToList()` copies the whole list only
  to change the static type — `IList` would avoid the copy and per-element dynamic binding.

---

## What looks good

- Consistent async-first design with thin sync wrappers, `ConfigureAwait(false)` throughout,
  and `CancellationToken` plumbing.
- `SqlConditionBuilder` correctly parameterizes values and resolves column names through mappings
  only — no injection surface from caller SQL fragments.
- Guard rails: `ValidateBulkDeleteRequest`'s refusal to delete-all without an explicit opt-in,
  and `EnsureBulkOperationsSupportEntityType` failing loudly on owned types instead of silently
  losing data.
- Resource tracking (`TempTableTracker`, `SqlResourceTracker`, `TrackedSqlBulkCopy`) with
  regression tests for the cancel-after-NOCHECK path.

---

## Recommended priorities

1. **#1** — per-table `try/catch` in the `finally` re-enable loop (correctness, error masking).
2. **#2** — weak or correctly-keyed mapping cache (memory leak).
3. **#4** — dead-transaction detection in constraint re-enable.
4. Remaining items as opportunistic cleanup.
