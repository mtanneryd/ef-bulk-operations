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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6.Model;
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.ClusteredIndexSort;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// SortUsingClusteredIndex must not KeyNotFound when the clustered index
    /// includes concurrency/computed columns absent from ColumnMappingByColumnName.
    /// </summary>
    [TestClass]
    public class BulkInsertClusteredIndexSortTests
    {
        [TestInitialize]
        public void Initialize()
        {
            using (var ctx = new ClusteredIndexSortContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
                ctx.Database.Create();
                // Default PK is clustered on Id only. Rebuild so the CI also includes
                // RowVersion (concurrency token, not in ColumnMappingByColumnName).
                ctx.Database.ExecuteSqlCommand(@"
DECLARE @pk sysname =
    (SELECT kc.name
     FROM sys.key_constraints kc
     WHERE kc.parent_object_id = OBJECT_ID(N'dbo.SortProbe') AND kc.type = N'PK');
EXEC(N'ALTER TABLE [dbo].[SortProbe] DROP CONSTRAINT [' + @pk + N']');
ALTER TABLE [dbo].[SortProbe] ADD CONSTRAINT [PK_SortProbe] PRIMARY KEY NONCLUSTERED ([Id]);
CREATE CLUSTERED INDEX [CX_SortProbe] ON [dbo].[SortProbe] ([Id], [RowVersion]);");
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            using (var ctx = new ClusteredIndexSortContext())
            {
                if (ctx.Database.Exists())
                    ctx.Database.Delete();
            }
        }

        [TestMethod]
        public void BulkInsert_WithSortUsingClusteredIndex_ShouldSucceed_WhenClusteredIndexIncludesRowVersion()
        {
            using (var db = new ClusteredIndexSortContext())
            {
                db.BulkInsertAll(new BulkInsertRequest<SortProbe>
                {
                    Entities = new[]
                    {
                        new SortProbe { Name = "bravo" },
                        new SortProbe { Name = "alpha" },
                    },
                    SortUsingClusteredIndex = true,
                });

                Assert.AreEqual(2, db.SortProbes.Count());
                CollectionAssert.AreEquivalent(
                    new[] { "alpha", "bravo" },
                    db.SortProbes.Select(p => p.Name).ToArray());
            }
        }
    }
}
