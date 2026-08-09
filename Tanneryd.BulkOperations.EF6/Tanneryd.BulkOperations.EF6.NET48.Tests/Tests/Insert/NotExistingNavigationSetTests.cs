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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EF6;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Insert
{
    /// <summary>
    /// Regression for issue #47: recursive insert must partition navigations into
    /// those with a set PK (existence-checked) and those without (always insert).
    /// Existing parents with a set PK must not enter the not-existing set — otherwise
    /// leftover-clearing would wipe a real identity PK and create a duplicate row.
    /// </summary>
    [TestClass]
    public class NotExistingNavigationSetTests
    {
        [TestMethod]
        public void BuildNotExistingNavigationSet_ShouldIncludeAll_WhenNoKeysAreSet()
        {
            var unsetA = new object();
            var unsetB = new object();
            var navs = new[] { unsetA, unsetB };

            var result = DbContextExtensions.BuildNotExistingNavigationSet(
                navs,
                navsWithSetKey: new HashSet<object>(),
                notExistingWithSetKey: null);

            CollectionAssert.AreEquivalent(navs, result.ToList());
        }

        [TestMethod]
        public void BuildNotExistingNavigationSet_ShouldTreatNullSetKeyCollectionAsEmpty()
        {
            var nav = new object();

            var result = DbContextExtensions.BuildNotExistingNavigationSet(
                new[] { nav },
                navsWithSetKey: null,
                notExistingWithSetKey: null);

            CollectionAssert.AreEquivalent(new[] { nav }, result.ToList());
        }

        [TestMethod]
        public void BuildNotExistingNavigationSet_ShouldKeepExistingParentsOut_WhenSelectNotExistingOmitsThem()
        {
            var existingParent = new object();
            var unsetNav = new object();
            var leftoverClientId = new object();

            var navs = new[] { existingParent, unsetNav, leftoverClientId };
            var navsWithSetKey = new HashSet<object> { existingParent, leftoverClientId };
            // Select-not-existing reports only the leftover client id as missing.
            var notExistingWithSetKey = new[] { leftoverClientId };

            var result = DbContextExtensions.BuildNotExistingNavigationSet(
                navs,
                navsWithSetKey,
                notExistingWithSetKey);

            CollectionAssert.AreEquivalent(
                new[] { unsetNav, leftoverClientId },
                result.ToList(),
                "Unset-key navs always insert; set-key leftovers insert; existing parents must not.");
            Assert.IsFalse(result.Contains(existingParent),
                "Existing parent must not be cleared/re-inserted (issue #47).");
        }

        [TestMethod]
        public void BuildNotExistingNavigationSet_ShouldBeEmpty_WhenEverySetKeyAlreadyExists()
        {
            var existingA = new object();
            var existingB = new object();
            var navs = new[] { existingA, existingB };
            var navsWithSetKey = new HashSet<object>(navs);

            var result = DbContextExtensions.BuildNotExistingNavigationSet(
                navs,
                navsWithSetKey,
                notExistingWithSetKey: Enumerable.Empty<object>());

            Assert.AreEqual(0, result.Count,
                "When every set-key nav already exists, nothing should be inserted.");
        }

        [TestMethod]
        public void BuildNotExistingNavigationSet_ShouldIncludeAllSetKeys_WhenNoneExistInDatabase()
        {
            var missingA = new object();
            var missingB = new object();
            var navs = new[] { missingA, missingB };
            var navsWithSetKey = new HashSet<object>(navs);

            var result = DbContextExtensions.BuildNotExistingNavigationSet(
                navs,
                navsWithSetKey,
                notExistingWithSetKey: navs);

            CollectionAssert.AreEquivalent(navs, result.ToList());
        }
    }
}
