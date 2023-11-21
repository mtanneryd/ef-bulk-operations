# Tanneryd.BulkOperations.EF6/Tanneryd.BulkOperations.EFCore

Nuget packages that extend the DbContext in EF6/EFCore with bulk operations.

## Getting Started

### Background
Read the CodeProject article [Bulk operations using Entity Framework](https://www.codeproject.com/Articles/1226978/Bulk-operations-using-Entity-Framework) if you are interested in some background.

### Prerequisites
The extension is built for, and requires, Entity Framework 6 or EF Core.

### Installing
 * EF6: Install the nuget package [Tanneryd.BulkOperations.EF6](https://www.nuget.org/packages/Tanneryd.BulkOperations.EF6). 
 * EFCore: Install the nuget package [Tanneryd.BulkOperations.EFCore](https://www.nuget.org/packages/Tanneryd.BulkOperations.EFCore). 

This will make the following methods available on the DbContext.

### API

#### Insert
```csharp
public class BulkInsertRequest<T>
{
    public IList<T> Entities { get; set; }
    public SqlTransaction Transaction { get; set; }
    public bool UpdateStatistics { get; set; } = false;
    public bool Recursive { get; set; } = false;
    public bool AllowNotNullSelfReferences { get; set; } = false;
    public bool SortUsingClusteredIndex { get; set; } = true;
}
```
* When UpdateStatistics is set the command "UPDATE STATISTICS <tablename> WITH ALL" will be executed after the insert.
* When Recursive is set to true the entire entity hierarchy will be inserted. 
* When AllowNotNullSelfReferences is set to true, entities with self referencing foreign keys declared as NOT NULL will be properly inserted. But, this will only work if the database user has the required privileges to execute **ALTER TABLE \<table name\> NOCHECK CONSTRAINT ALL** and **ALTER TABLE \<table name\> CHECK CONSTRAINT ALL**.
* When SortUsingClusteredIndex is set to true the entities will be sorted according to the clustered index of the target table.

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
    public string[] UpdatedPropertyNames { get; set; }
    public string[] KeyPropertyNames { get; set; }
    public SqlTransaction Transaction { get; set; }
    public bool InsertIfNew { get; set; }
}
```

* If UpdatedPropertyNames is an empty list all non-key mapped columns will be updated, otherwise only the columns specified.
* If KeyPropertyNames is an empty list the primary key columns will be used to select which rows to update, otherwise the columns specified will be used.

```csharp
 public static BulkOperationResponse BulkUpdateAll(
     this DbContext ctx,
     BulkUpdateRequest request)
```

#### Select
##### Select

##### SelectExisting
The select-existing feature provides a way to identify the subset of existing or non-existing items in a local collection where an item is considered as existing if it is equal to an entity saved in the database according to a set of defined key properties. This provides a very efficient way of figuring out which items in your local collection needs to be inserted and which to be updated. The item collection can be of the same type as the EF entity but it does not have to be.
```csharp
public class BulkSelectRequest<T>
{
	public BulkSelectRequest(string[] keyPropertyNames, 
				 IList<T> items = null,
				 SqlTransaction transaction = null)
	{
        KeyPropertyMappings = keyPropertyNames.Select(n => new KeyPropertyMapping
            {
                ItemPropertyName = n,
                EntityPropertyName = n
            })
            .ToArray();
        Items = items;
        Transaction = transaction;
    }
    public IList<T> Items { get; set; }
    public KeyPropertyMapping[] KeyPropertyMappings { get; set; }
    public SqlTransaction Transaction { get; set; }
   

    public BulkSelectRequest()
    {
        KeyPropertyMappings = new KeyPropertyMapping[0];
        Items = new T[0];
    }
}

public class KeyPropertyMapping
{
    public string ItemPropertyName { get; set; }
    public string EntityPropertyName { get; set; }

    public static KeyPropertyMapping[] IdentityMappings(string[] names)
    {
        return names.Select(n => new KeyPropertyMapping
        {
            ItemPropertyName = n,
            EntityPropertyName = n
        }).ToArray();
    }
}
```


```csharp
/// <summary>
/// Given a set of entities we return the subset of these entities
/// that already exist in the database, according to the key selector
/// used.
/// </summary>
/// <typeparam name="T1">The item collection type</typeparam>
/// <typeparam name="T2">The EF entity type</typeparam>
/// <param name="ctx"></param>
/// <param name="request"></param>
public static IList<T1> BulkSelectExisting<T1,T2>(
    this DbContext ctx,
    BulkSelectRequest<T1> request)
```

#### Delete

##### DeleteExisting
NOT IMPLEMENTED
##### DeleteNotExisting
```csharp
    public class BulkDeleteRequest<T>
    {
        public SqlCondition[] SqlConditions { get; set; }

        public KeyPropertyMapping[] KeyPropertyMappings { get; set; }
        public IList<T> Items { get; set; }
        public SqlTransaction Transaction { get; set; }
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public BulkDeleteRequest(
            SqlCondition[] sqlConditions,
            string[] keyPropertyNames,
            IList<T> items = null,
            SqlTransaction transaction = null)
        {
            SqlConditions = sqlConditions;
            KeyPropertyMappings = KeyPropertyMapping.IdentityMappings(keyPropertyNames);
            Items = items;
            Transaction = transaction;
        }

        public BulkDeleteRequest()
        {
            KeyPropertyMappings = new KeyPropertyMapping[0];
            Items = new T[0];
        }
    }
```

```csharp
        /// <summary>
        /// The bulk delete request contains a SqlCondition. It has
        /// a list of column name/column value pairs and will be used
        /// to build an AND where clause. This method will delete any
        /// rows in the database that matches this SQL condition unless
        /// it also matches one of the supplied entities according to
        /// the key selector used.
        ///
        /// !!! IMPORTANT !!!
        /// MAKE SURE THAT YOU FULLY UNDERSTAND THIS LOGIC
        /// BEFORE USING THE BULK DELETE METHOD SO THAT YOU
        /// DO NOT END UP WITH AN EMPTY DATABASE.
        /// 
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        public static void BulkDeleteNotExisting<T1, T2>(
            this DbContext ctx,
            BulkDeleteRequest<T1> request)
```
## Release history
##### 2.0.5 (2023-11-22)
 * Added .NET8 target for EF Core.
##### 2.0.3 (2023-09-05)
 * Updated the README file to better reflect that there are two packages, one for EF6 and one for EF Core. No functional changes at all.
##### 2.0.2 (2023-08-10)
 * BulkInsert to tables having a DateTime foreign key did not work as expected.
##### 2.0.1 (2023-08-07)
 * Fixed a bug in BulkUpdateAll when column names are not identical with entity property names.
 
##### 2.0.0 (2023-06-19)
 * Merged EF6 and EF Core packages into one solution and one github project.
 
##### 1.4.1 (2021-11-12)
 * Added support for retrieving values from the database (updating the local entities that have matching data in the database) when doing a BulkSelectExisting.
	
##### 1.4.0 (2020-06-15)
 * Added experimental support for TPH table inheritance.
 * The package now targets both netstandard2.1 and net45
 * Fixed a bug when using computed columns in tables without identity primary keys (reported and resolved by https://github.com/hzahradnik)

##### 1.3.0 (2019-12-21)
 * Bugfix: More fixes related to parsing table names in some very specific situations.
 * Added support for the recompile option and for deleting the query plan cache.

##### 1.2.7 (2019-09-13)
 * Bugfix: Issue #18 - Bug when parsing table names. (not completely fixed in 1.2.5)
 * Added documentation for BulkDeleteNotExisting

##### 1.2.5 (2019-07-15)
 * Bugfix: Issue #18 - Bug when parsing table names.
 * Bugfix: Sorting on a clustered index did not work for tables with schemas.
 * Bugfix: BulkSelectExisting and BulkSelectNotExisting sometimes returned duplicates.

##### 1.2.4 (2019-05-26)
 * Bugfix: Issue #16 - BulkInsert is not thread safety.

##### 1.2.3 (2019-03-29)
 * Bugfix: Join tables with Guid keys misbehaved.
 * Added method BulkDeleteNotExisting

##### 1.2.2 (2018-12-01)
 * Bugfix: BulkSelect did not work properly with null columns.
 * Bugfix: Contexts using lazy loading and thus dynamic proxies did not work as expected.
 * Bugfix: Tables with Guid primary keys did not work as expected in some situations.


## Built With

* Visual Studio 2022

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [tags on this repository](https://github.com/your/project/tags). 

## Authors

* **Måns Tånneryd** 

## License

This project is licensed under the Apache License - see the [LICENSE.md](LICENSE.md) file for details.
