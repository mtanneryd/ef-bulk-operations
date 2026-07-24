/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.StringStoreGeneratedPk;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// SelectNewEntities must use typed unset checks for store-generated
    /// non-Guid PKs. Comparing a string PK with dynamic == 0 throws
    /// RuntimeBinderException before any rows are inserted.
    /// </summary>
    [TestClass]
    public class BulkInsertStringStoreGeneratedPkTests
    {
        [TestInitialize]
        public void Initialize()
        {
            using (var ctx = new StringStoreGeneratedPkContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
                // Avoid Database.Create(): EF6 would emit IDENTITY on nvarchar.
                // Create an empty database, then the real table with a DEFAULT.
                var master = new SqlConnectionStringBuilder(StringStoreGeneratedPkContext.ConnectionString)
                {
                    InitialCatalog = "master"
                };
                using (var conn = new SqlConnection(master.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
IF DB_ID(N'Tanneryd.BulkOperations.EF6.NET48.Tests.StringStoreGeneratedPk') IS NULL
    CREATE DATABASE [Tanneryd.BulkOperations.EF6.NET48.Tests.StringStoreGeneratedPk]";
                        cmd.ExecuteNonQuery();
                    }
                }

                ctx.Database.ExecuteSqlCommand(@"
IF OBJECT_ID(N'dbo.CatalogItem', N'U') IS NOT NULL DROP TABLE [dbo].[CatalogItem];
CREATE TABLE [dbo].[CatalogItem] (
    [Code] nvarchar(36) NOT NULL CONSTRAINT [DF_CatalogItem_Code] DEFAULT CONVERT(nvarchar(36), NEWID()),
    [Name] nvarchar(max) NULL,
    CONSTRAINT [PK_dbo.CatalogItem] PRIMARY KEY ([Code])
);");
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            using (var ctx = new StringStoreGeneratedPkContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
            }
        }

        [TestMethod]
        public void BulkInsert_ShouldSucceed_WhenStoreGeneratedStringPrimaryKeyIsUnset()
        {
            using (var db = new StringStoreGeneratedPkContext())
            {
                var item = new CatalogItem
                {
                    // Empty string is "unset" for string keys; null fails SqlBulkCopy
                    // into a NOT NULL staging column.
                    Code = "",
                    Name = "widget",
                };

                db.BulkInsertAll(new BulkInsertRequest<CatalogItem>
                {
                    Entities = new[] { item },
                    SortUsingClusteredIndex = false,
                });

                Assert.AreEqual(1, db.CatalogItems.Count());
                Assert.IsFalse(
                    string.IsNullOrEmpty(item.Code),
                    "Store-generated string PK must be written back via MERGE OUTPUT.");
                Assert.AreEqual(item.Code, db.CatalogItems.Single().Code);
            }
        }

        [TestMethod]
        public void BulkInsert_ShouldSkip_WhenStoreGeneratedStringPrimaryKeyIsAlreadySet()
        {
            using (var db = new StringStoreGeneratedPkContext())
            {
                var item = new CatalogItem
                {
                    Code = "SKU-1",
                    Name = "preassigned",
                };

                db.BulkInsertAll(new BulkInsertRequest<CatalogItem>
                {
                    Entities = new[] { item },
                    SortUsingClusteredIndex = false,
                });

                Assert.AreEqual(
                    0,
                    db.CatalogItems.Count(),
                    "SelectNewEntities must treat a non-empty string PK as already set and skip insert.");
            }
        }
    }
}
