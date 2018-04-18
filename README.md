# Tanneryd.BulkOperations.EF6

A nuget package that extends the DbContext in EF6 with bulk operations for both inserts and updates.

## Getting Started

Read the CodeProject article [Bulk operations using Entity Framework](https://www.codeproject.com/Articles/1226978/Bulk-operations-using-Entity-Framework) if you are interested in some background. Currently this extension requires that the database user has enough privileges to execute ALTER TABLE <table_name> NOCHECK CONSTRAINT ALL. The reason for this has to do with tables having self referencing foreign keys set to be NOT NULL. The current release (1.1.0-beta7) resolves this in a rather brutish way. In 1.1.0-beta8 I will make support for these self referencing things optional and thus. The privilege requirements will go back to normal for all other uses.

### Prerequisites

The extension is built for, and requires, Entity Framework 6 and .NET 4.5 or later.

### Installing

Install the nuget package [Tanneryd.BulkOperations.EF6](https://www.nuget.org/packages/Tanneryd.BulkOperations.EF6). This will make the following methods available on the DbContext.

    /// <summary>
    /// 
    /// The request object properties have the following function:
    /// 
    /// Entities - The entities are mapped to rows in a table and these table rows will be updated.
    /// UpdatedColumnNames - Specifies which columns to update. An empty list will update ALL columns.
    /// KeyMemberNames - Specifies which columns to use as row selectors. An empty list will result
    ///                  in the primary key columns to be used.
    /// Transaction - If a transaction object is provided the update will be made within that transaction.
    /// InsertIfNew - When set to true, any entities new to the table will be inserted. Otherwise they 
    ///               will be ignored.
    /// 
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="request"></param>
    public static BulkOperationResponse BulkUpdateAll(
        this DbContext ctx,
        BulkUpdateRequest request)


    /// <summary>
    /// 
    /// The request object properties have the following function:
    /// 
    ///  Entities - The entities are mapped to rows in a table and these table rows will 
    ///             be updated.
    ///  Transaction - If a transaction object is provided the update will be made within that transaction.
    ///  Recursive - If true any new entities added to navigation properties will also be inserted. Foreign 
    ///              key relationships will be honored for both new and existing entities in the entire 
    ///              entity graph.
    /// 
    /// </summary>
    /// <param name="ctx"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    public static BulkOperationResponse BulkInsertAll<T>(
        this DbContext ctx,
        BulkInsertRequest<T> request)


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





