using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using issue_31_reproduction.dbmodel;
using Tanneryd.BulkOperations.EF6.NetStd;

namespace issue_31_reproduction
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				string dbName = "issue31repro";
				string dataSource = "(localdb)\\MSSQLLocalDB";
				//string dataSource = "localhost\\SQLEXPRESS";

				string connStrFormat = $"Data Source={dataSource};Initial Catalog={{0}};Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
				EnsureLocalDb(connStrFormat, dbName);
				var dbModel = new DbModelContainer($"metadata=res://*/dbmodel.DbModel.csdl|res://*/dbmodel.DbModel.ssdl|res://*/dbmodel.DbModel.msl;provider=System.Data.SqlClient;provider connection string='{string.Format(connStrFormat, dbName)}'");
				var logItems = new List<LogItem>()
				{
					CreateLogItemInstance(),
					CreateLogItemInstance(),
					CreateLogItemInstance()
				};
				Console.WriteLine($"Before bulk insert - Table Log contains {dbModel.Log.Count()} items");
				dbModel.BulkInsertAll(logItems);
				Console.WriteLine($"After bulk insert - Table Log contains {dbModel.Log.Count()} items");
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

			Console.WriteLine("Press any key to exit...");
			Console.ReadKey();
		}

		static void EnsureLocalDb(string connStringFormat, string dbName)
		{
			ExecQuery(string.Format(connStringFormat, "master"), $"If(db_id(N'{dbName}') IS NULL) BEGIN CREATE DATABASE [{dbName}]; END; ");
			ExecQuery(string.Format(connStringFormat, dbName), System.IO.File.ReadAllText("create_script.sql"));
		}

		static void ExecQuery(string connString, string query)
		{
			using (SqlConnection connection = new SqlConnection(
				connString))
			{
				SqlCommand command = new SqlCommand(
					query, connection);
				connection.Open();
				using (SqlDataReader reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						Console.WriteLine(String.Format("{0}, {1}",
							reader[0], reader[1]));
					}
				}
			}
		}

		static LogItem CreateLogItemInstance()
		{
			return new LogItem()
			{
				ConfigId = DateTime.UtcNow.Ticks,
				Exception = "my exception",
				Message = "my message",
				CorrelationId = Guid.NewGuid(),
				CrmUser = Guid.NewGuid(),
				Date = DateTime.UtcNow,
				EntityId = Guid.NewGuid(),
				EntityLogicalName = "myentity",
				Level = "DEBUG",
				Logger = "Db integration test",
				OrganizationId = Guid.NewGuid(),
				SdkMessage = "TestMessage",
				Thread = "1",
				Id = 0
			};
		}
	}
}
