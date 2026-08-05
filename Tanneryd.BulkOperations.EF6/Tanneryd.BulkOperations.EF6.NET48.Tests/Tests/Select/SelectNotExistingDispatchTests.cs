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
using System.Collections;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Select
{
    /// <summary>
    /// Regression: BulkSelectNotExistingByTypeAsync caches a CreateDelegate invoker
    /// per entity type and must forward insert CommandTimeout / UseTableLock onto the
    /// strongly-typed BulkSelectRequest (defaults are 1 minute / false).
    /// No SQL Server required — covers the reflection cache and request shaping only.
    /// </summary>
    [TestClass]
    public class SelectNotExistingDispatchTests
    {
        [TestMethod]
        public void GetSelectNotExistingInvoker_ShouldCacheSameDelegate_PerEntityType()
        {
            var first = DbContextExtensions.GetSelectNotExistingInvoker(typeof(DispatchProbeA));
            var second = DbContextExtensions.GetSelectNotExistingInvoker(typeof(DispatchProbeA));

            Assert.IsNotNull(first);
            Assert.AreSame(first, second,
                "Repeated lookups for the same CLR type must reuse the cached invoker.");
        }

        [TestMethod]
        public void GetSelectNotExistingInvoker_ShouldCreateDistinctDelegates_ForDifferentTypes()
        {
            var a = DbContextExtensions.GetSelectNotExistingInvoker(typeof(DispatchProbeA));
            var b = DbContextExtensions.GetSelectNotExistingInvoker(typeof(DispatchProbeB));

            Assert.AreNotSame(a, b,
                "Different entity types must not share a closed generic invoker.");
        }

        [TestMethod]
        public void CreateSelectNotExistingRequest_ShouldForwardTimeoutAndTableLock()
        {
            var entities = (IList)new List<DispatchProbeA>
            {
                new DispatchProbeA { Id = 1 },
            };
            var timeout = TimeSpan.FromMinutes(7);

            var request = DbContextExtensions.CreateSelectNotExistingRequest<DispatchProbeA>(
                entities,
                keyPropertyNames: new[] { nameof(DispatchProbeA.Id) },
                sqlTransaction: null,
                commandTimeout: timeout,
                useTableLock: true);

            Assert.AreEqual(timeout, request.CommandTimeout,
                "Insert CommandTimeout must not fall back to BulkSelectRequest's 1-minute default.");
            Assert.IsTrue(request.UseTableLock,
                "Insert UseTableLock must be forwarded onto the select-not-existing staging copy.");
            Assert.AreEqual(1, request.Items.Count);
            Assert.AreEqual(nameof(DispatchProbeA.Id), request.KeyPropertyMappings[0].ItemPropertyName);
        }

        [TestMethod]
        public void CreateSelectNotExistingRequest_ShouldKeepExplicitDefaults_WhenInsertUsesDefaults()
        {
            var entities = (IList)new List<DispatchProbeA>();

            var request = DbContextExtensions.CreateSelectNotExistingRequest<DispatchProbeA>(
                entities,
                keyPropertyNames: new[] { nameof(DispatchProbeA.Id) },
                sqlTransaction: null,
                commandTimeout: TimeSpan.FromMinutes(1),
                useTableLock: false);

            Assert.AreEqual(TimeSpan.FromMinutes(1), request.CommandTimeout);
            Assert.IsFalse(request.UseTableLock);
            Assert.AreEqual(0, request.Items.Count);
        }

        private sealed class DispatchProbeA
        {
            public int Id { get; set; }
        }

        private sealed class DispatchProbeB
        {
            public int Id { get; set; }
        }
    }
}
