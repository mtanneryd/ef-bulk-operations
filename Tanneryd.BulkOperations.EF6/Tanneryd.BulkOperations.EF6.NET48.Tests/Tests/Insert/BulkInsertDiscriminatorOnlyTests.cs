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
using Tanneryd.BulkOperations.EF6.NET48.Tests.Models.EF.DiscriminatorOnly;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// Regression for review finding H3: when retrieving identity keys for a
    /// TPH type with no non-key columns, MERGE SELECT lists discriminator+rowno
    /// but the USING alias only declares rowno.
    /// </summary>
    [TestClass]
    public class BulkInsertDiscriminatorOnlyTests
    {
        [TestInitialize]
        public void Initialize()
        {
            using var ctx = new DiscriminatorOnlyContext();
            if (ctx.Database.Exists())
                ctx.Database.Delete();
            ctx.Database.Create();
        }

        [TestCleanup]
        public void Cleanup()
        {
            using var ctx = new DiscriminatorOnlyContext();
            if (ctx.Database.Exists())
                ctx.Database.Delete();
        }

        [TestMethod]
        public void BulkInsert_ShouldRetrieveIdentityKeys_ForTphTypeWithOnlyDiscriminatorColumns()
        {
            using var db = new DiscriminatorOnlyContext();

            var tags = new TagBase[]
            {
                new RedTag(),
                new RedTag(),
                new BlueTag(),
            };

            db.BulkInsertAll(new BulkInsertRequest<TagBase>
            {
                Entities = tags,
                EnableRecursiveInsert = EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
            });

            Assert.IsTrue(tags.All(t => t.Id > 0), "Expected store-generated identity keys to be written back.");
            Assert.AreEqual(2, db.RedTags.Count());
            Assert.AreEqual(1, db.BlueTags.Count());
            Assert.AreEqual(3, db.Tags.Count());
        }
    }
}
