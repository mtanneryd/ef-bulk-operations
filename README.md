# Tanneryd.BulkOperations.EF6

A nuget package that extends the DbContext in EF6 with bulk operations for both inserts and updates.

## Getting Started

Read the CodeProject article [Bulk operations using Entity Framework](https://www.codeproject.com/Articles/1226978/Bulk-operations-using-Entity-Framework) if you are interested in some background.

### Prerequisites

The extension is built for, and requires, Entity Framework 6 and .NET 4.5 or later.

### Installing

Install the nuget package [Tanneryd.BulkOperations.EF6](https://www.nuget.org/packages/Tanneryd.BulkOperations.EF6). This will make the following methods available on the DbContext.

### Using

#### Insert
```csharp
public class BulkInsertRequest<T>
{
    public IList<T> Entities { get; set; }
    public SqlTransaction Transaction { get; set; }
    public bool Recursive { get; set; }
    public bool AllowNotNullSelfReferences { get; set; }
}
```

* When Recursive is set to true the entire entity hierarchy will be inserted. 
* When AllowNotNullSelfReferences is set to true, entities with self referencing foreign keys declared as NOT NULL will be properly inserted. But, this will only work if the database user has the required privileges to execute **ALTER TABLE \<table name\> NOCHECK CONSTRAINT ALL** and **ALTER TABLE \<table name\> CHECK CONSTRAINT ALL**.

```csharp
 public static BulkOperationResponse BulkInsertAll<T>(
     this DbContext ctx,
     BulkInsertRequest<T> request)
```

#### Update
```csharp
public class BulkUpdateRequest
{
    public IList Entities { get; set; }
    public string[] UpdatedColumnNames { get; set; }
    public string[] KeyMemberNames { get; set; }
    public SqlTransaction Transaction { get; set; }
    public bool InsertIfNew { get; set; }
}
```

* If UpdatedColumnNames is an empty list all non-key mapped columns will be updated, otherwise only the columns specified.
* If KeyMemberNames is an empty list the primary key columns will be used to select which rows to update, otherwise the columns specified will be used.

```csharp
 public static BulkOperationResponse BulkUpdateAll(
     this DbContext ctx,
     BulkUpdateRequest request)
```

## Built With

* Visual Studio 2017
* .NET Framework 4.5
* Entity Framework 6.0.2

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [tags on this repository](https://github.com/your/project/tags). 

## Authors

* **Måns Tånneryd** 

## License

This project is licensed under the Apache License - see the [LICENSE.md](LICENSE.md) file for details.





