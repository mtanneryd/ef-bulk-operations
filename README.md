# Tanneryd.BulkOperations.EF6 / EFCore

NuGet packages that extend `DbContext` with high-throughput SQL Server bulk operations (insert, update, select, delete) for Entity Framework 6 and EF Core.

## Getting started

### Background

See the CodeProject article [Bulk operations using Entity Framework](https://web.archive.org/web/20250820221347/https://www.codeproject.com/Articles/1226978/Bulk-Operations-using-Entity-Framework) (Internet Archive; CodeProject is offline) for design background.

### Install

| Target | Package |
|--------|---------|
| EF6 (.NET Framework 4.8) | [Tanneryd.BulkOperations.EF6](https://www.nuget.org/packages/Tanneryd.BulkOperations.EF6) |
| EF Core | [Tanneryd.BulkOperations.EFCore](https://www.nuget.org/packages/Tanneryd.BulkOperations.EFCore) |

Both packages target **SQL Server**. EF Core uses **Microsoft.Data.SqlClient**. EF6 accepts either Microsoft.Data.SqlClient or the legacy System.Data.SqlClient connection that classic EF6 apps usually get.

#### EF6 SqlClient support

EF6 resolves `ctx.Database.Connection` (or an `EntityConnection` store connection) and runs bulk work against whichever concrete type is present:

| Connection type | Supported | Notes |
|-----------------|-----------|-------|
| `Microsoft.Data.SqlClient.SqlConnection` | Yes (recommended) | Preferred going forward; required if you pass a `Microsoft.Data.SqlClient.SqlTransaction` on the request. |
| `System.Data.SqlClient.SqlConnection` | Yes | Works for default EF6 + `EntityFramework.SqlServer` setups without migrating the provider. |

You do **not** need to migrate off System.Data.SqlClient for bulk operations to work. The library keeps a dual facade for commands and `SqlBulkCopy`.

**Caveat:** request `Transaction` is typed as `Microsoft.Data.SqlClient.SqlTransaction`. That value cannot be used when the context still owns a legacy System.Data.SqlClient connection — pass `null` (library opens/uses its own work) or migrate the context to Microsoft.Data.SqlClient first.

##### Optional: switch EF6 to Microsoft.Data.SqlClient

To make EF6 create a real `Microsoft.Data.SqlClient.SqlConnection`, register `MicrosoftSqlDbConfiguration`, set `providerName="Microsoft.Data.SqlClient"` on the connection string, and reference:

```csharp
using System.Data.Entity;
using System.Data.Entity.SqlServer;

[DbConfigurationType(typeof(MicrosoftSqlDbConfiguration))]
public class MyContext : DbContext
{
    // ...
}
```

```xml
<PackageReference Include="EntityFramework" Version="6.5.2" />
<PackageReference Include="Microsoft.EntityFramework.SqlServer" Version="6.5.2" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.2" />
```

```xml
<entityFramework>
  <providers>
    <provider invariantName="Microsoft.Data.SqlClient"
              type="System.Data.Entity.SqlServer.MicrosoftSqlProviderServices, Microsoft.EntityFramework.SqlServer" />
  </providers>
</entityFramework>

<system.data>
  <DbProviderFactories>
    <add name="Microsoft.Data.SqlClient"
         invariant="Microsoft.Data.SqlClient"
         description=".NET Framework Data Provider for SqlServer"
         type="Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient" />
  </DbProviderFactories>
</system.data>
```

Config entries alone are not enough: without `MicrosoftSqlDbConfiguration` (or equivalent `codeConfigurationType`), EF6 often still creates a System.Data.SqlClient connection even when Microsoft.Data.SqlClient is listed. That is fine for this library — the legacy path handles it.

## API overview

All operations are extension methods on `DbContext`. Sync methods block on async implementations with `ConfigureAwait(false)`. Prefer the `*Async` APIs from async call sites (ASP.NET Core, async controllers, etc.).

| Operation | Sync | Async |
|-----------|------|-------|
| Insert | `BulkInsertAll` | `BulkInsertAllAsync` |
| Update | `BulkUpdateAll` | `BulkUpdateAllAsync` |
| Select rows | `BulkSelect` | `BulkSelectAsync` |
| Select existing items | `BulkSelectExisting` | `BulkSelectExistingAsync` |
| Select missing items | `BulkSelectNotExisting` | `BulkSelectNotExistingAsync` |
| Delete not existing | `BulkDeleteNotExisting` | `BulkDeleteNotExistingAsync` |
| Update statistics | `UpdateStatistics` | `UpdateStatisticsAsync` |

Namespaces: `Tanneryd.BulkOperations.EF6` / `Tanneryd.BulkOperations.EFCore` (and `.Model`).

---

### Bulk insert

Uses `SqlBulkCopy`. For tables with a **single store-generated primary key**, generated values can be written back onto your entities (via a temp table + `MERGE … OUTPUT`).

Supported store-generated key modes (EF6 and EF Core) — used for the MERGE … OUTPUT key-retrieval path:

| Mode | EF6 | EF Core |
|------|-----|---------|
| SQL Server `IDENTITY` | `DatabaseGeneratedOption.Identity` on integer keys | `UseIdentityColumn()` / `IdentityColumn` strategy |
| Guid (or other) DB default | `DatabaseGeneratedOption.Identity` on Guid (migration emits `NEWSEQUENTIALID()`) | `ValueGeneratedOnAdd()` **plus** `HasDefaultValueSql("NEWSEQUENTIALID()")` (or similar) |
| Computed PK | `DatabaseGeneratedOption.Computed` | `ValueGeneratedOnAddOrUpdate` |

Not treated as store-generated: client-side generators (EF Core bare `ValueGeneratedOnAdd` on Guid without a SQL default, **SequenceHiLo**), user-assigned keys (`None` / `ValueGeneratedNever`), and composite keys. For those, supply key values before bulk insert (or rely on the non-identity insert path).

```csharp
var numbers = Enumerable.Range(1, 10_000)
    .Select(i => new Number { Value = i })
    .ToList();

var response = ctx.BulkInsertAll(new BulkInsertRequest<Number>
{
    Entities = numbers,
    // EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys, // default
    // SortUsingClusteredIndex = true,  // default
    // UpdateStatistics = false,        // default
});

// With identity PKs and the default EnableRecursiveInsert option,
// each entity's generated key is set on the local instance:
Console.WriteLine(numbers[0].Id);
```

#### `BulkInsertRequest<T>`

| Property | Default | Meaning |
|----------|---------|---------|
| `Entities` | — | Rows to insert. |
| `Transaction` | `null` | Optional `SqlTransaction`. |
| `EnableRecursiveInsert` | `NoButRetrieveGeneratedPrimaryKeys` | See below. |
| `AllowNotNullSelfReferences` | `No` | When `Yes`, temporarily runs `ALTER TABLE … NOCHECK CONSTRAINT ALL` so not-null self-FK graphs can insert (requires `ALTER TABLE` rights). Constraints are re-enabled afterwards. |
| `SortUsingClusteredIndex` | `true` | Sort rows by the target table clustered index before copy. |
| `UpdateStatistics` | `false` | Run `UPDATE STATISTICS <table> WITH ALL` after insert. |
| `UseTableLock` | `false` | Take a table lock (`TABLOCK`) during the bulk operation for higher throughput at the cost of concurrency. |
| `CommandTimeout` | 30 minutes | Command timeout for SQL after the bulk copy. |

#### `EnableRecursiveInsert`

| Value | Behavior |
|-------|----------|
| `NoButRetrieveGeneratedPrimaryKeys` | Insert only the supplied entities; retrieve store-generated PKs onto them. |
| `NoAndIgnoreGeneratedPrimaryKeys` | Insert only; skip PK retrieval (faster single `SqlBulkCopy`). |
| `Yes` | Walk navigation properties and insert the full entity graph (FKs honored for new and existing related rows). |

```csharp
// Insert a parent and its children in one call
var blog = new Blog
{
    Name = "My Blog",
    Posts = { new Post { Title = "Hello" }, new Post { Title = "World" } }
};

ctx.BulkInsertAll(new BulkInsertRequest<Blog>
{
    Entities = new[] { blog },
    EnableRecursiveInsert = EnableRecursiveInsert.Yes
});
```

Async:

```csharp
await ctx.BulkInsertAllAsync(request, cancellationToken);
```

---

### Bulk update

Stages rows in a temp table, then `UPDATE`s the target on key match. Optionally inserts rows that do not match when `InsertIfNew` is set.

```csharp
foreach (var price in prices)
    price.Value += 1;

ctx.BulkUpdateAll(new BulkUpdateRequest
{
    Entities = prices,
    // KeyPropertyNames empty → use table primary key
    UpdatedPropertyNames = new[] { nameof(Price.Value) },
    InsertIfNew = false
});
```

#### `BulkUpdateRequest`

| Property | Default | Meaning |
|----------|---------|---------|
| `Entities` | — | Rows to update. |
| `UpdatedPropertyNames` | empty | CLR properties to update. Empty = all mapped non-key columns. |
| `KeyPropertyNames` | empty | CLR properties used as the match key. Empty = primary key. |
| `InsertIfNew` | `false` | Insert entities that do not match an existing row. |
| `Transaction` | `null` | Optional `SqlTransaction`. |
| `UseTableLock` | `false` | Take a table lock (`TABLOCK`) during the bulk operation. |
| `CommandTimeout` | 30 minutes | SQL command timeout. |

```csharp
await ctx.BulkUpdateAllAsync(request, cancellationToken);
```

---

### Bulk select

#### `BulkSelect<T1, T2>`

Match rows in table `T2` from a list of `T1` items and key mappings; return the matched `T2` entities. Useful when the selector is **composite** (for a single column, EF `Contains` is often enough).

**NULL matching:** for nullable key columns, a `NULL` key value matches rows where the column `IS NULL`. Non-nullable key columns use plain equality. This applies to `BulkSelect`, `BulkSelectExisting`, `BulkSelectNotExisting`, and `BulkDeleteNotExisting`.

```csharp
// Composite key lookup: match Price rows by Date + Name
var keys = new List<Price>
{
    new Price { Date = new DateTime(2019, 1, 1), Name = "ERICB" },
    new Price { Date = new DateTime(2019, 1, 2), Name = "ERICB" },
};

var matched = ctx.BulkSelect<Price, Price>(
    new BulkSelectRequest<Price>(new[] { "Date", "Name" }, keys));
```

#### `BulkSelectExisting` / `BulkSelectNotExisting`

Partition a local collection into items that do / do not already exist in the database according to the key selector. Ideal for “which to insert vs update” without loading the whole table.

- `T1` — item collection type (can differ from the EF entity)
- `T2` — EF entity / table type

```csharp
var candidates = /* local Price rows */;

var existing = ctx.BulkSelectExisting<Price, Price>(
    new BulkSelectRequest<Price>(new[] { "Date", "Name", "Value" }, candidates));

var missing = ctx.BulkSelectNotExisting<Price, Price>(
    new BulkSelectRequest<Price>(new[] { "Date", "Name", "Value" }, candidates));

ctx.BulkInsertAll(new BulkInsertRequest<Price> { Entities = missing });
ctx.BulkUpdateAll(new BulkUpdateRequest { Entities = existing.ToList() });
```

#### `BulkSelectRequest<T>`

| Property | Meaning |
|----------|---------|
| `Items` | Local collection to match. |
| `KeyPropertyMappings` | How item properties map to entity properties for the match. Use `KeyPropertyMapping.IdentityMappings(names)` when names are identical. |
| `ColumnPropertyMappings` | Optional. For `BulkSelectExisting` only: copy matched DB column values onto the local items. |
| `Transaction` | Optional `SqlTransaction`. |
| `UseTableLock` | Take a table lock (`TABLOCK`) during staging. Default `false`. |
| `CommandTimeout` | Default 1 minute. |

```csharp
await ctx.BulkSelectExistingAsync<Price, Price>(request, cancellationToken);
await ctx.BulkSelectNotExistingAsync<Price, Price>(request, cancellationToken);
await ctx.BulkSelectAsync<MyKey, Price>(request, cancellationToken);
```

---

### Bulk delete not existing

Deletes database rows that match a set of `SqlCondition` filters **and** do **not** appear in the supplied `Items` list (according to the key mapping).

**Warning:** An empty `Items` list is rejected by default. To delete every row in the condition window, set `AllowDeleteAllMatchingConditions = true` (and still review the conditions carefully).

```csharp
// Keep these people; delete other people that match the filter
var keepers = new[] { personA, personB };

ctx.BulkDeleteNotExisting<Person, Person>(new BulkDeleteRequest<Person>
{
    SqlConditions = new[]
    {
        new SqlCondition("LastName", "Tånneryd")
    },
    KeyPropertyMappings = KeyPropertyMapping.IdentityMappings(new[] { "Id" }),
    Items = keepers
});

// Explicit opt-in: delete every row matching the condition window
ctx.BulkDeleteNotExisting<Person, Person>(new BulkDeleteRequest<Person>
{
    SqlConditions = new[] { new SqlCondition("MotherId", motherId) },
    KeyPropertyMappings = KeyPropertyMapping.IdentityMappings(new[] { "Id" }),
    Items = Array.Empty<Person>(),
    AllowDeleteAllMatchingConditions = true
});
```

`SqlCondition` values are parameterized (`null` / `DBNull` → `IS NULL`). Column names may be table column names or mapped property names.

```csharp
await ctx.BulkDeleteNotExistingAsync<Person, Person>(request, cancellationToken);
```

`BulkDeleteExisting` is not implemented.

---

### Update statistics

Runs `UPDATE STATISTICS <table> WITH ALL` for the mapped table of `T`. The same helper is used when `BulkInsertRequest.UpdateStatistics` is `true`.

```csharp
ctx.UpdateStatistics<Number>();
await ctx.UpdateStatisticsAsync<Number>(TimeSpan.FromMinutes(5), cancellationToken);
```

---

### Async notes

- Async methods await SQL Server I/O (`OpenAsync`, `ExecuteNonQueryAsync`, `ExecuteReaderAsync`, `WriteToServerAsync`) and accept an optional `CancellationToken`.
- Sync APIs remain supported as thin wrappers that block with `ConfigureAwait(false).GetAwaiter().GetResult()`.
- From ASP.NET Framework sync pages/actions, sync APIs are fine (deadlock-safe with this library). From already-async code, prefer `*Async` so you do not block a thread that holds a sync context or request resources.
- Internal helpers used on bulk hot paths are async-only; they do not nest additional sync-over-async under the public sync wrappers.

---

### Transactions

Pass a `Microsoft.Data.SqlClient.SqlTransaction` on the request when you need the bulk work to participate in an ambient transaction you opened on the same connection. For EF6 this requires the context connection to already be a `Microsoft.Data.SqlClient.SqlConnection` (see [EF6 SqlClient support](#ef6-sqlclient-support)).

```csharp
using var connection = (SqlConnection)ctx.Database.Connection;
if (connection.State != ConnectionState.Open)
    connection.Open();

using var tx = connection.BeginTransaction();
try
{
    ctx.BulkInsertAll(new BulkInsertRequest<Number>
    {
        Entities = numbers,
        Transaction = tx
    });
    tx.Commit();
}
catch
{
    tx.Rollback();
    throw;
}
```

For EF Core, use `(SqlConnection)ctx.Database.GetDbConnection()` the same way.


## Release history

##### 4.0.0 (2026-07-20)
 * Added async bulk operation APIs for EF6 and EF Core (`BulkInsertAllAsync`, `BulkUpdateAllAsync`, `BulkSelectAsync`, `BulkSelectExistingAsync`, `BulkSelectNotExistingAsync`, `BulkDeleteNotExistingAsync`, `UpdateStatisticsAsync`, and related helpers).
 * Async APIs await SQL Server I/O and accept an optional `CancellationToken`.
 * Existing synchronous methods remain unchanged and delegate to the async implementations.
 * Nullable key columns now use null-aware matching in `BulkSelect`, `BulkSelectExisting`, `BulkSelectNotExisting`, and `BulkDeleteNotExisting`: a `NULL` key value matches rows where the column `IS NULL` (previously `NULL` never matched).
 * Safer optimistic-concurrency handling in `BulkUpdateAll`: precise row accounting and hardened transaction rollback/cleanup.
 * Performance improvements in row materialization for bulk copy staging.
 * XML documentation is now included in the packages (IntelliSense).
 * Updated NuGet package dependencies.
 * Added VS Code build and test tasks.
 * Minor code cleanup.

##### 3.0.0 (2025-04-30)
 * Using Microsoft.Data.SqlClient instead of System.Data.SqlClient for EF6.
 * Added support for .NET 4.8.
 * Removed support for .NET Standard, .NET Framework 4.5 and .NET Framework 4.7.
 * Removed support for EDMX model-first in EF6.

##### 2.0.5 (2023-11-22)
 * Added .NET 8 target for EF Core.

##### 2.0.3 (2023-09-05)
 * Updated the README to clarify that there are two packages (EF6 and EF Core). No functional changes.

##### 2.0.2 (2023-08-10)
 * BulkInsert to tables having a DateTime foreign key did not work as expected.

##### 2.0.1 (2023-08-07)
 * Fixed a bug in BulkUpdateAll when column names are not identical with entity property names.

##### 2.0.0 (2023-06-19)
 * Merged EF6 and EF Core packages into one solution and one GitHub project.

##### 1.4.1 (2021-11-12)
 * Added support for retrieving values from the database (updating local entities that match) when doing BulkSelectExisting.

##### 1.4.0 (2020-06-15)
 * Added experimental support for TPH table inheritance.
 * The package now targets both netstandard2.1 and net45.
 * Fixed a bug when using computed columns in tables without identity primary keys (reported and resolved by https://github.com/hzahradnik).

##### 1.3.0 (2019-12-21)
 * Bugfix: More fixes related to parsing table names in some very specific situations.
 * Added support for the recompile option and for deleting the query plan cache.

##### 1.2.7 (2019-09-13)
 * Bugfix: Issue #18 - Bug when parsing table names (not completely fixed in 1.2.5).
 * Added documentation for BulkDeleteNotExisting.

##### 1.2.5 (2019-07-15)
 * Bugfix: Issue #18 - Bug when parsing table names.
 * Bugfix: Sorting on a clustered index did not work for tables with schemas.
 * Bugfix: BulkSelectExisting and BulkSelectNotExisting sometimes returned duplicates.

##### 1.2.4 (2019-05-26)
 * Bugfix: Issue #16 - BulkInsert is not thread-safe.

##### 1.2.3 (2019-03-29)
 * Bugfix: Join tables with Guid keys misbehaved.
 * Added method BulkDeleteNotExisting.

##### 1.2.2 (2018-12-01)
 * Bugfix: BulkSelect did not work properly with null columns.
 * Bugfix: Contexts using lazy loading and thus dynamic proxies did not work as expected.
 * Bugfix: Tables with Guid primary keys did not work as expected in some situations.

## Built with

* Visual Studio 2026

## Versioning

We use [SemVer](http://semver.org/) for versioning. For available versions, see the [tags on this repository](https://github.com/mtanneryd/ef-bulk-operations/tags).

## Authors

* **Måns Tånneryd**

## Support

If these packages help you ship bulk SQL Server work with EF6 or EF Core, consider [sponsoring the project on GitHub](https://github.com/sponsors/mtanneryd). Sponsorships fund ongoing maintenance, compatibility updates, tests, and docs.

## License

This project is licensed under the Apache License — see the [LICENSE.md](LICENSE.md) file for details.
