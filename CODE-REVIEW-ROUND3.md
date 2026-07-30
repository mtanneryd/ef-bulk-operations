# Code Review — Round 3 (Remaining Improvement Opportunities)

Date: third-pass review after all items from `CODE-REVIEW.md` and
`CODE-REVIEW-FOLLOWUP.md` were resolved (except the intentionally open
sync-over-async wrappers, which are excluded here as well).

Items are ordered roughly by value.

---

## Correctness / robustness

### 1. Nullable reference types are not enabled (EFCore project)

`Tanneryd.BulkOperations.EFCore.csproj` has no `<Nullable>enable</Nullable>`.
Enabling NRT (even incrementally, starting with `<Nullable>annotations</Nullable>`)
would surface latent null-handling bugs at compile time.

- **Impact:** High (latent bug detection)
- **Effort:** Large — requires annotating the whole public and internal surface.
- **Suggestion:** Consider for a dedicated hardening pass or the next major version.

### 2. Redundant `IS NULL` conditions on non-nullable key columns

There is a `TODO` in `DbContextExtensions.Select.cs` (~line 104, and mirrored
in other select paths / EF6): the generated join predicate emits

```sql
([t1].[X] = [t2].[X] OR ([t1].[X] IS NULL AND [t2].[X] IS NULL))
```

for **every** key column. For non-nullable columns the `OR ... IS NULL` branch
is dead logic that defeats index seeks and can degrade the `EXCEPT` /
join queries significantly on large temp tables.

- **Impact:** Medium–High (query performance on bulk select paths)
- **Effort:** Small — column nullability is available in the mappings; emit the
  plain `=` form for non-nullable keys.
- **Suggestion:** Implement, with regression tests covering nullable and
  non-nullable key matching (including the existing null-vs-zero semantics tests).

### 3. Swallowed rollback exception

`DbContextExtensions.Update.cs` (~line 276):

```csharp
try { ownedTransaction?.Rollback(); } catch { /* ignore */ }
```

A bare catch-all hides genuinely unexpected failures. Prefer checking
`SqlTransactionHelper.IsZombied` first and/or narrowing to
`InvalidOperationException`.

- **Impact:** Low–Medium (diagnosability)
- **Effort:** Trivial

---

## Performance (minor)

### 4. Per-row `List<object>` + `ToArray()` in `ObjectListDataReader` value factories

E.g. `DbContextExtensions.Select.cs` (~line 93) and the insert paths in both
projects: each row allocates a `List<object>`, then copies it via `ToArray()`.
Pre-sizing an `object[]` of known length (`keyProperties.Length + 1`) and
filling it directly removes two allocations per row on hot bulk paths.

- **Impact:** Low–Medium (allocation pressure on large batches)
- **Effort:** Small

### 5. `string.Join` predicate construction inside loops

Mostly one-time per operation; low priority. Only worth touching if the
surrounding code is edited anyway.

- **Impact:** Low
- **Effort:** Trivial

---

## API / design (future major version)

### 6. `new TableColumn[0]` occurrences

Several call sites allocate empty arrays with `new TableColumn[0]`.
Use `Array.Empty<TableColumn>()` (or collection expression `[]`) —
zero-allocation and consistent with modern style.

- **Impact:** Low
- **Effort:** Trivial

### 7. XML doc coverage on public model types

`BulkInsertRequest`, `BulkUpdateRequest`, and related request/response types
have thin XML documentation for a published NuGet package. Adding
`<GenerateDocumentationFile>true</GenerateDocumentationFile>` would also
flag the gaps as warnings.

- **Impact:** Low (consumer experience)
- **Effort:** Medium

---

## Tooling

### 8. No analyzers configured

Neither library project sets `<AnalysisLevel>`. Adding
`<AnalysisLevel>latest-recommended</AnalysisLevel>` (and optionally
`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` for the library
projects) would prevent regressions of the kinds fixed in earlier rounds.

- **Impact:** Medium (long-term quality)
- **Effort:** Small to enable; medium to burn down the initial warning list.

---

## Recommended action order

1. **#2** — real query-performance win, small testable diff.
2. **#4** — genuine allocation win on hot paths, small diff.
3. **#6** — trivial cleanup.
4. **#3** — trivial diagnosability improvement.
5. **#8** — enable analyzers, triage warnings.
6. **#1 / #7** — larger commitments; schedule deliberately.

## Explicitly out of scope

- Sync-over-async wrappers (`.ConfigureAwait(false).GetAwaiter().GetResult()`)
  — intentionally retained; see `CODE-REVIEW-FOLLOWUP.md`.
