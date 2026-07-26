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

using System;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Mappings
{
    /// <summary>
    /// Regression: the per-model MappingsExtractor cache must
    /// - reuse the same extractor for the same IModel across context instances,
    /// - never retain the DbContext instance that first populated it (a cached
    ///   extractor outliving a disposed context must not root that context),
    /// - keep entries collectible together with their model (weak cache), so
    ///   dynamically built models do not leak.
    /// No database connection is required; only the model is built.
    /// </summary>
    [TestClass]
    public class MappingExtractorCacheTests
    {
        private const string DummyConnectionString =
            @"data source=(localdb)\MSSQLLocalDB;initial catalog=MappingExtractorCacheTests;Integrated Security=SSPI;TrustServerCertificate=true";

        private static readonly DbContextOptions<CacheProbeContext> SharedOptions =
            new DbContextOptionsBuilder<CacheProbeContext>()
                .UseSqlServer(DummyConnectionString)
                .Options;

        [TestMethod]
        public void GetMappingExtractor_ShouldReturnSameInstance_ForSameModel()
        {
            using var ctx1 = new CacheProbeContext(SharedOptions);
            using var ctx2 = new CacheProbeContext(SharedOptions);

            Assert.AreSame(ctx1.Model, ctx2.Model,
                "Precondition: contexts sharing options must share the model.");

            var extractor1 = DbContextExtensions.GetMappingExtractor(ctx1);
            var extractor2 = DbContextExtensions.GetMappingExtractor(ctx2);

            Assert.AreSame(extractor1, extractor2,
                "Contexts sharing the same IModel must share one cached extractor.");
        }

        [TestMethod]
        public void GetMappingExtractor_ShouldNotRetainDbContext()
        {
            var (contextRef, extractor) = CreateContextAndGetExtractor();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.IsNotNull(extractor); // Keep the cached extractor strongly reachable.
            Assert.IsFalse(contextRef.IsAlive,
                "The cached MappingsExtractor must not root the DbContext that created it.");
        }

        [TestMethod]
        public void GetMappingExtractor_ShouldProduceWorkingMappings()
        {
            using var ctx = new CacheProbeContext(SharedOptions);

            var extractor = DbContextExtensions.GetMappingExtractor(ctx);
            var mappings = extractor.GetMappings(typeof(CacheProbeEntity));

            Assert.AreEqual("Probes", mappings.TableName.Name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (WeakReference contextRef, MappingsExtractor extractor) CreateContextAndGetExtractor()
        {
            var ctx = new CacheProbeContext(SharedOptions);
            var extractor = DbContextExtensions.GetMappingExtractor(ctx);
            var contextRef = new WeakReference(ctx);
            ctx.Dispose();
            return (contextRef, extractor);
        }

        public class CacheProbeEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class CacheProbeContext : DbContext
        {
            public CacheProbeContext(DbContextOptions<CacheProbeContext> options)
                : base(options)
            {
            }

            public DbSet<CacheProbeEntity> Probes { get; set; }
        }
    }
}
