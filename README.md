# Tanneryd.BulkOperations.EF6

A nuget package that extends the DbContext in EF6 with bulk operations for both inserts and updates.

This repository is still under construction. So, even though the code most likely works there will definately be inconsistencies between this README anbd the code.

## Getting Started

Read the CodeProject article [Bulk operations using Entity Framework](https://www.codeproject.com/Articles/1226978/Bulk-operations-using-Entity-Framework) if you are interested in some background.

### Prerequisites

The extension is built for, and requires, Entity Framework 6 and .NET 4.5 or later.

### Installing

Install the nuget package [Tanneryd.BulkOperations.EF6](https://www.nuget.org/packages/Tanneryd.BulkOperations.EF6). This will make the following two methods available on the DbContext.

	/// <summary>
	/// Update all entities using a temp table and 
	/// System.Data.SqlClient.SqlBulkCopy.
	/// Only tables with primary keys will be updated.
	/// </summary>
	/// <param name="ctx"></param>
	/// <param name="entities"></param>
	/// <param name="updatedColumns">If defined, only these columns 
	/// will be updated.</param>
	/// <param name="transaction"></param>
	public static void BulkUpdateAll(
		this DbContext ctx,
		IList entities,
		string[] updatedColumns = null,
		SqlTransaction transaction = null) 


	/// <summary>
	/// Insert all entities using System.Data.SqlClient.SqlBulkCopy. 
	/// </summary>
	/// <param name="ctx"></param>
	/// <param name="entities"></param>
	/// <param name="transaction"></param>
	/// <param name="recursive">True if the entire entity graph should be inserted, false otherwise.</param>
	public static void BulkInsertAll(
		this DbContext ctx,
		IList entities,
		SqlTransaction transaction = null,
		bool recursive = false)

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





