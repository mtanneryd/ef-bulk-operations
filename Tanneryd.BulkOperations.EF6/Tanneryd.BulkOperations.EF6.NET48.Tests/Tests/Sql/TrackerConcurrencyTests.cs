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
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tanneryd.BulkOperations.Common.Sql;

namespace Tanneryd.BulkOperations.EF6.NET48.Tests.Tests.Sql
{
    /// <summary>
    /// Regression: tracker scopes flow via AsyncLocal into parallel child tasks, so
    /// create/dispose/drop counters must use Interlocked increments. Plain ++ loses
    /// updates under concurrent Notify* calls and makes dispose/cleanup assertions flaky.
    /// </summary>
    [TestClass]
    public class TrackerConcurrencyTests
    {
        private const int TaskCount = 32;
        private const int NotificationsPerTask = 250;
        private const int Expected = TaskCount * NotificationsPerTask;

        [TestMethod]
        public async Task SqlResourceTracker_ShouldCountAllConcurrentNotifications()
        {
            using var scope = SqlResourceTracker.BeginScope();

            await Task.WhenAll(Enumerable.Range(0, TaskCount).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < NotificationsPerTask; i++)
                {
                    SqlResourceTracker.NotifyCreated();
                    SqlResourceTracker.NotifyDisposed();
                }
            })));

            Assert.AreEqual(Expected, scope.Created);
            Assert.AreEqual(Expected, scope.Disposed);
        }

        [TestMethod]
        public async Task TempTableTracker_ShouldCountAllConcurrentNotifications()
        {
            using var scope = TempTableTracker.BeginScope();

            await Task.WhenAll(Enumerable.Range(0, TaskCount).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < NotificationsPerTask; i++)
                {
                    TempTableTracker.NotifyCreated();
                    TempTableTracker.NotifyDropped();
                }
            })));

            Assert.AreEqual(Expected, scope.Created);
            Assert.AreEqual(Expected, scope.Dropped);
        }

        [TestMethod]
        public async Task SqlTransactionTracker_ShouldCountAllConcurrentNotifications()
        {
            using var scope = SqlTransactionTracker.BeginScope();

            await Task.WhenAll(Enumerable.Range(0, TaskCount).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < NotificationsPerTask; i++)
                {
                    SqlTransactionTracker.NotifyCreated();
                    SqlTransactionTracker.NotifyDisposed();
                }
            })));

            Assert.AreEqual(Expected, scope.Created);
            Assert.AreEqual(Expected, scope.Disposed);
        }
    }
}
