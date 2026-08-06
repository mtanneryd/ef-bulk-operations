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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Select
{
    /// <summary>
    /// Regression: BulkSelect staging previously created SqlBulkCopy with
    /// CreateBulkCopy defaults only. Request CommandTimeout / UseTableLock must
    /// be forwarded on every select staging path (BulkSelect, Existing,
    /// NotExisting, DeleteNotExisting).
    /// </summary>
    [TestClass]
    public class SelectStagingBulkCopySettingsTests
    {
        [TestMethod]
        public void GetSelectStagingBulkCopySettings_ShouldForwardTimeoutAndTableLock()
        {
            var request = new BulkSelectRequest<StagingProbe>(new[] { nameof(StagingProbe.Id) })
            {
                CommandTimeout = TimeSpan.FromMinutes(9),
                UseTableLock = true,
            };

            var settings = DbContextExtensions.GetSelectStagingBulkCopySettings(request);

            Assert.AreEqual(TimeSpan.FromMinutes(9), settings.Timeout,
                "BulkSelect staging must not fall back to CreateBulkCopy's 10-minute default.");
            Assert.IsTrue(settings.UseTableLock,
                "BulkSelect staging must honor opt-in TABLOCK from the request.");
        }

        [TestMethod]
        public void GetSelectStagingBulkCopySettings_ShouldKeepRequestDefaults()
        {
            var request = new BulkSelectRequest<StagingProbe>(new[] { nameof(StagingProbe.Id) });

            var settings = DbContextExtensions.GetSelectStagingBulkCopySettings(request);

            Assert.AreEqual(TimeSpan.FromMinutes(1), settings.Timeout,
                "BulkSelectRequest default timeout is 1 minute.");
            Assert.IsFalse(settings.UseTableLock);
        }

        [TestMethod]
        public void GetSelectStagingBulkCopySettings_ShouldThrow_ForNullRequest()
        {
            Assert.ThrowsExactly<ArgumentNullException>(
                () => DbContextExtensions.GetSelectStagingBulkCopySettings<StagingProbe>((BulkSelectRequest<StagingProbe>)null));
        }

        [TestMethod]
        public void GetSelectStagingBulkCopySettings_ShouldForwardDeleteRequestTimeoutAndTableLock()
        {
            var request = new BulkDeleteRequest<StagingProbe>
            {
                CommandTimeout = TimeSpan.FromMinutes(4),
                UseTableLock = true,
            };

            var settings = DbContextExtensions.GetSelectStagingBulkCopySettings(request);

            Assert.AreEqual(TimeSpan.FromMinutes(4), settings.Timeout);
            Assert.IsTrue(settings.UseTableLock);
        }

        private sealed class StagingProbe
        {
            public int Id { get; set; }
        }
    }
}
