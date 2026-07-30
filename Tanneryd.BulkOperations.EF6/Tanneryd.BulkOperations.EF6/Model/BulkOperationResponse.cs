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
using System.Collections.Generic;

namespace Tanneryd.BulkOperations.EF6.Model
{
    /// <summary>
    /// Base response for bulk operations with timing and affected-row counts.
    /// </summary>
    public class BulkOperationResponse
    {
        /// <summary>
        /// Total wall-clock time for the operation.
        /// </summary>
        public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Affected row count per concrete entity type.
        /// </summary>
        public List<Tuple<Type, long>> AffectedRows { get; set; } = new List<Tuple<Type, long>>();
    }

    /// <summary>
    /// Response for bulk insert with per-type statistics and constraint diagnostics.
    /// </summary>
    public class BulkInsertResponse : BulkOperationResponse
    {
        /// <summary>
        /// Per-type timing statistics for the bulk copy and insert-into phases.
        /// </summary>
        public List<Tuple<Type, BulkInsertStatistics>> BulkInsertStatistics { get; set; } =
            new List<Tuple<Type, BulkInsertStatistics>>();

        /// <summary>
        /// Tables whose CHECK/FK constraints could not be re-enabled WITH CHECK
        /// and remain marked as not trusted.
        /// </summary>
        public List<string> TablesWithNoCheckConstraints { get; set; } = new List<string>();

        /// <summary>
        /// Time spent running UPDATE STATISTICS, when requested.
        /// </summary>
        public TimeSpan? TimeElapsedDuringUpdateStatistics { get; set; }

        /// <summary>
        /// Time spent sorting entities by clustered index, when enabled.
        /// </summary>
        public TimeSpan? TimeElapsedDuringSorting { get; set; }

        /// <summary>
        /// Produces human-readable summary lines for logging.
        /// </summary>
        public string[] Report()
        {
            var report = new List<string>();
            if (TimeElapsedDuringUpdateStatistics.HasValue)
                report.Add(
                    $@"BulkOperations - UPDATE STATISTICS executed in {TimeElapsedDuringUpdateStatistics.Value.TotalSeconds:f0} seconds.");
            if (TimeElapsedDuringSorting.HasValue)
                report.Add(
                    $@"BulkOperations - Sorted columns in {TimeElapsedDuringSorting.Value.TotalSeconds:f0} seconds.");
            foreach (var r in AffectedRows)
            {
                report.Add($"BulkOperations - {r.Item2} rows affected for {r.Item1.Name}");
            }

            foreach (var stat in BulkInsertStatistics)
            {
                report.Add(
                    $"BulkOperations - {stat.Item1.Name} - BulkCopy={stat.Item2.TimeElapsedDuringBulkCopy.TotalSeconds}, InsertInto={stat.Item2.TimeElapsedDuringInsertInto.TotalSeconds}");
            }

            return report.ToArray();
        }
    }

    /// <summary>
    /// Timing statistics for a single entity type during bulk insert.
    /// </summary>
    public struct BulkInsertStatistics
    {
        /// <summary>Time spent in SqlBulkCopy.</summary>
        public TimeSpan TimeElapsedDuringBulkCopy { get; set; }
        /// <summary>Time spent in the INSERT INTO / MERGE phase.</summary>
        public TimeSpan TimeElapsedDuringInsertInto { get; set; }
    }
}