/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EFCore.Tests.UnitTests.Select
{
    /// <summary>
    /// SQL-shape tests for the nullable-aware key join predicate. Nullable
    /// columns get the OR (IS NULL AND IS NULL) branch, non-nullable columns
    /// use plain equality so SQL Server can use index seeks.
    /// </summary>
    [TestClass]
    public class KeyJoinConditionTests
    {
        [TestMethod]
        public void BuildKeyJoinCondition_ShouldUsePlainEquality_ForNonNullableColumn()
        {
            var sql = DbContextExtensions.BuildKeyJoinCondition("Id", isNullable: false, "t0", "t1");

            Assert.AreEqual("([t0].[Id] = [t1].[Id])", sql);
        }

        [TestMethod]
        public void BuildKeyJoinCondition_ShouldIncludeNullMatchBranch_ForNullableColumn()
        {
            var sql = DbContextExtensions.BuildKeyJoinCondition("Value", isNullable: true, "t0", "t1");

            Assert.AreEqual(
                "([t0].[Value] = [t1].[Value] OR ([t0].[Value] IS NULL AND [t1].[Value] IS NULL))",
                sql);
        }
    }
}
