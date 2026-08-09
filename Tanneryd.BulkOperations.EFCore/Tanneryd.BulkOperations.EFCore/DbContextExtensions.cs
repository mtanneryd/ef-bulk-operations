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

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.EFCore.Model;
using IColumnMapping = Microsoft.EntityFrameworkCore.Metadata.IColumnMapping;

namespace Tanneryd.BulkOperations.EFCore
{
    /// <summary>
    /// SQL Server bulk operation extension methods for EF Core <see cref="DbContext"/>.
    /// </summary>
    /// <remarks>
    /// Prefer the <c>*Async</c> APIs from async call sites. Synchronous methods are
    /// intentional thin wrappers that block with
    /// <c>ConfigureAwait(false).GetAwaiter().GetResult()</c>; avoid nesting further
    /// sync-over-async inside those paths.
    /// </remarks>
    public static partial class DbContextExtensions
    {
        private static readonly HashSet<Type> IntegerTypes = new HashSet<Type>
        {
            typeof(Int16),
            typeof(Int32),
            typeof(Int64),
            typeof(UInt16),
            typeof(UInt32),
            typeof(UInt64),
        };

        // Weak per-model cache: entries become collectible together with their
        // IModel, so dynamically built models (per-tenant options, test model
        // churn) do not leak. ConditionalWeakTable is thread-safe.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IModel, MappingsExtractor>
            _mappingExtractorsByModel = new System.Runtime.CompilerServices.ConditionalWeakTable<IModel, MappingsExtractor>();

        #region Public API

        /// <summary>
        /// Clears the SQL Server plan cache via <c>DBCC FREEPROCCACHE</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Warning:</b> <c>DBCC FREEPROCCACHE</c> (without a plan handle) flushes
        /// the plan cache for the entire SQL Server instance, not just the current
        /// database or connection. That forces recompiles across all workloads on
        /// the instance and can cause a temporary CPU spike.
        /// </para>
        /// <para>
        /// Use only for plan-cache troubleshooting or diagnostics—never as part of
        /// routine application flow.
        /// </para>
        /// </remarks>
        public static void DeleteAllExecutionPlansFromCache(this DbContext ctx, SqlTransaction sqlTransaction)
        {
            DeleteAllExecutionPlansFromCacheAsync(ctx, sqlTransaction).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="DeleteAllExecutionPlansFromCache"/>
        public static Task DeleteAllExecutionPlansFromCacheAsync(
            this DbContext ctx,
            SqlTransaction sqlTransaction,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            var query = "DBCC FREEPROCCACHE WITH NO_INFOMSGS";
            return DeleteAllExecutionPlansFromCacheCoreAsync(ctx, query, sqlTransaction, cancellationToken);
        }

        private static async Task DeleteAllExecutionPlansFromCacheCoreAsync(
            DbContext ctx,
            string query,
            SqlTransaction sqlTransaction,
            CancellationToken cancellationToken)
        {
            var connection = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
            using var cmd = CreateSqlCommand(query, connection, sqlTransaction, TimeSpan.FromSeconds(30));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes database rows that match <see cref="BulkDeleteRequest{T}.SqlConditions"/>
        /// and do not appear in <see cref="BulkDeleteRequest{T}.Items"/> according to the
        /// key mapping. Empty <c>Items</c> is rejected unless
        /// <see cref="BulkDeleteRequest{T}.AllowDeleteAllMatchingConditions"/> is true.
        ///
        /// !!! IMPORTANT !!!
        /// MAKE SURE THAT YOU FULLY UNDERSTAND THIS LOGIC
        /// BEFORE USING THE BULK DELETE METHOD SO THAT YOU
        /// DO NOT END UP WITH AN EMPTY DATABASE.
        /// 
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        public static void BulkDeleteNotExisting<T1, T2>(
            this DbContext ctx,
            BulkDeleteRequest<T1> request)
        {
            BulkDeleteNotExistingAsync<T1, T2>(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="BulkDeleteNotExisting{T1,T2}"/>
        public static Task BulkDeleteNotExistingAsync<T1, T2>(
            this DbContext ctx,
            BulkDeleteRequest<T1> request,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            ValidateBulkDeleteRequest(request);
            return DoBulkDeleteNotExistingAsync<T1, T2>(ctx, request, cancellationToken);
        }

        /// <summary>
        /// The request object contains a mapping between properties in T1
        /// and properties in T2. BulkSelect will match all rows in the T2
        /// table given the list of T1 items and their defined key
        /// properties and return the set of T2 items matched. This is
        /// particularly useful when you need to select a number of rows
        /// from a table using multiple selector columns. If there is only
        /// one column you need to select on you could simply use the EF
        /// equivalent of 'where in', Contains() or Any() on your local
        /// collection holding the values you want to apply 'where in' on.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public static IList<T2> BulkSelect<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request) where T2 : new()
        {
            return BulkSelectAsync<T1, T2>(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="BulkSelect{T1,T2}"/>
        public static Task<IList<T2>> BulkSelectAsync<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request,
            CancellationToken cancellationToken = default) where T2 : new()
        {
            ValidateDbContext(ctx);
            ValidateBulkSelectRequest(request);
            return DoBulkSelectAsync<T1, T2>(ctx, request, cancellationToken);
        }

        /// <summary>
        /// Given a set of entities we return the subset of these entities
        /// that already exist in the database, according to the key selector
        /// used.
        /// </summary>
        /// <typeparam name="T1">The item collection type</typeparam>
        /// <typeparam name="T2">The EF entity type</typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        public static IList<T1> BulkSelectExisting<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request)
        {
            return BulkSelectExistingAsync<T1, T2>(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="BulkSelectExisting{T1,T2}"/>
        public static Task<IList<T1>> BulkSelectExistingAsync<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            ValidateBulkSelectRequest(request);
            return DoBulkSelectExistingAsync<T1, T2>(ctx, request, cancellationToken);
        }

        /// <summary>
        /// Given a set of entities we return the subset of these entities
        /// that do not exist in the database, according to the key selector
        /// used.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        public static IList<T1> BulkSelectNotExisting<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request)
        {
            return BulkSelectNotExistingAsync<T1, T2>(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="BulkSelectNotExisting{T1,T2}"/>
        public static Task<IList<T1>> BulkSelectNotExistingAsync<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            ValidateBulkSelectRequest(request);
            return DoBulkSelectNotExistingAsync<T1, T2>(ctx, request, cancellationToken);
        }

        /// <summary>
        /// Runtime-typed wrapper used by the insert path when the entity CLR type is
        /// only known dynamically (must await, not block via the sync API).
        /// Forwards the insert request's CommandTimeout and UseTableLock so the
        /// select-not-existing staging copy does not fall back to BulkSelectRequest defaults.
        /// Reflection (closing the generic core method) runs once per entity type;
        /// subsequent calls go through a cached delegate.
        /// </summary>
        private static Task<IList> BulkSelectNotExistingByTypeAsync(
            DbContext ctx,
            Type t,
            IList entities,
            TableColumnMapping[] pkColumnMappings,
            SqlTransaction sqlTransaction,
            TimeSpan commandTimeout,
            bool useTableLock,
            CancellationToken cancellationToken = default)
        {
            var invoker = GetSelectNotExistingInvoker(t);
            var keyPropertyNames = pkColumnMappings.Select(m => m.EntityProperty.Name).ToArray();
            return invoker(ctx, entities, keyPropertyNames, sqlTransaction, commandTimeout, useTableLock, cancellationToken);
        }

        /// <summary>
        /// Cached per-entity-type delegate used by <see cref="BulkSelectNotExistingByTypeAsync"/>.
        /// Exposed for unit tests so the CreateDelegate cache can be verified without SQL Server.
        /// </summary>
        internal static SelectNotExistingInvoker GetSelectNotExistingInvoker(Type entityType)
        {
            return _selectNotExistingInvokersByType.GetOrAdd(entityType, static type =>
                (SelectNotExistingInvoker)typeof(DbContextExtensions)
                    .GetMethod(nameof(BulkSelectNotExistingCoreAsync), BindingFlags.NonPublic | BindingFlags.Static)
                    .MakeGenericMethod(type)
                    .CreateDelegate(typeof(SelectNotExistingInvoker)));
        }

        internal delegate Task<IList> SelectNotExistingInvoker(
            DbContext ctx,
            IList entities,
            string[] keyPropertyNames,
            SqlTransaction sqlTransaction,
            TimeSpan commandTimeout,
            bool useTableLock,
            CancellationToken cancellationToken);

        private static readonly ConcurrentDictionary<Type, SelectNotExistingInvoker> _selectNotExistingInvokersByType =
            new ConcurrentDictionary<Type, SelectNotExistingInvoker>();

        /// <summary>
        /// Builds the strongly-typed request used by the runtime-typed select-not-existing
        /// path, forwarding insert CommandTimeout / UseTableLock (not BulkSelectRequest defaults).
        /// </summary>
        internal static BulkSelectRequest<T> CreateSelectNotExistingRequest<T>(
            IList entities,
            string[] keyPropertyNames,
            SqlTransaction sqlTransaction,
            TimeSpan commandTimeout,
            bool useTableLock)
        {
            return new BulkSelectRequest<T>(keyPropertyNames, entities.Cast<T>().ToArray(), sqlTransaction)
            {
                CommandTimeout = commandTimeout,
                UseTableLock = useTableLock,
            };
        }

        private static async Task<IList> BulkSelectNotExistingCoreAsync<T>(
            DbContext ctx,
            IList entities,
            string[] keyPropertyNames,
            SqlTransaction sqlTransaction,
            TimeSpan commandTimeout,
            bool useTableLock,
            CancellationToken cancellationToken)
        {
            var request = CreateSelectNotExistingRequest<T>(
                entities, keyPropertyNames, sqlTransaction, commandTimeout, useTableLock);

            var result = await BulkSelectNotExistingAsync<T, T>(ctx, request, cancellationToken).ConfigureAwait(false);
            return result as IList ?? result.ToList();
        }

        /// <summary>
        /// All columns of the entities' corresponding table rows 
        /// will be updated using the table primary key. If a 
        /// transaction object is provided the update will be made
        /// within that transaction. Tables with no primary key will
        /// be left untouched.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="entities"></param>
        /// <param name="transaction"></param>
        public static BulkOperationResponse BulkUpdateAll(
            this DbContext ctx,
            IList entities,
            SqlTransaction transaction)
        {
            return BulkUpdateAllAsync(ctx, entities, transaction).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="BulkUpdateAll(DbContext, IList, SqlTransaction)"/>
        public static Task<BulkOperationResponse> BulkUpdateAllAsync(
            this DbContext ctx,
            IList entities,
            SqlTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            var request = new BulkUpdateRequest
            {
                Entities = entities,
                Transaction = transaction,
            };
            return BulkUpdateAllAsync(ctx, request, cancellationToken);
        }

        /// <summary>
        /// Updates table rows from the request entities.
        /// Entities — rows to update.
        /// UpdatedPropertyNames — CLR properties to update (empty = all non-key columns).
        /// KeyPropertyNames — CLR properties used as the match key (empty = primary key).
        /// Transaction — optional ambient transaction.
        /// InsertIfNew — when true, unmatched entities are inserted.
        /// </summary>
        public static BulkOperationResponse BulkUpdateAll(
            this DbContext ctx,
            BulkUpdateRequest request)
        {
            return BulkUpdateAllAsync(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="BulkUpdateAll(DbContext, BulkUpdateRequest)"/>
        public static async Task<BulkOperationResponse> BulkUpdateAllAsync(
            this DbContext ctx,
            BulkUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            ValidateBulkUpdateRequest(request);

            var response = new BulkOperationResponse();
            if (request.Entities.Count == 0) return response;
            await DoBulkUpdateAllAsync(ctx, request, response, cancellationToken).ConfigureAwait(false);

            return response;
        }


        /// <summary>
        /// Inserts all entities using SqlBulkCopy.
        /// </summary>
        /// <param name="recursive">When true, inserts the full entity graph (EnableRecursiveInsert.Yes).</param>
        public static BulkInsertResponse BulkInsertAll<T>(
            this DbContext ctx,
            IList<T> entities,
            SqlTransaction transaction = null,
            bool recursive = false)
        {
            return BulkInsertAllAsync(ctx, entities, transaction, recursive).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="BulkInsertAll{T}(DbContext, IList{T}, SqlTransaction, bool)"/>
        public static Task<BulkInsertResponse> BulkInsertAllAsync<T>(
            this DbContext ctx,
            IList<T> entities,
            SqlTransaction transaction = null,
            bool recursive = false,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            var request = new BulkInsertRequest<T>
            {
                Entities = entities,
                Transaction = transaction,
                EnableRecursiveInsert = recursive ? EnableRecursiveInsert.Yes : EnableRecursiveInsert.NoButRetrieveGeneratedPrimaryKeys,
            };
            return BulkInsertAllAsync(ctx, request, cancellationToken);
        }

        /// <summary>
        /// Inserts entities via SqlBulkCopy.
        /// Entities — rows to insert.
        /// Transaction — optional ambient transaction.
        /// EnableRecursiveInsert — whether to walk navigations and/or retrieve generated PKs.
        /// AllowNotNullSelfReferences — temporarily disable CHECK/FK constraints when needed.
        /// UpdateStatistics / SortUsingClusteredIndex — post-insert stats and pre-copy sort.
        /// </summary>
        public static BulkInsertResponse BulkInsertAll<T>(
            this DbContext ctx,
            BulkInsertRequest<T> request)
        {
            return BulkInsertAllAsync(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="BulkInsertAll{T}(DbContext, BulkInsertRequest{T})"/>
        public static async Task<BulkInsertResponse> BulkInsertAllAsync<T>(
            this DbContext ctx,
            BulkInsertRequest<T> request,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            ValidateBulkInsertRequest(request);

            var response = new BulkInsertResponse();

            if (request.Entities.Count == 0) return response;

            var s = new Stopwatch();
            s.Start();

            var succeeded = false;
            try
            {
                // Mixed batches are split per concrete type further down the pipeline,
                // so sorting and statistics must also be applied per type group rather
                // than assuming the first entity's table represents the whole batch.
                var extractor = GetMappingExtractor(ctx);
                var typeGroups = request.Entities.GroupBy(e => e.GetType()).ToList();
                var mappingsByType = new Dictionary<Type, Mappings>();
                if (request.SortUsingClusteredIndex)
                {
                    var s0 = new Stopwatch();
                    s0.Start();
                    var sortedEntities = new List<T>(request.Entities.Count);
                    foreach (var typeGroup in typeGroups)
                    {
                        var groupType = typeGroup.Key;
                        var groupEntities = typeGroup.ToList();
                        if (!mappingsByType.TryGetValue(groupType, out var mappings))
                        {
                            mappings = extractor.GetMappings(groupType);
                            mappingsByType.Add(groupType, mappings);
                        }

                        var groupTableName = extractor.GetTableName(ctx, groupType);
                        var clusteredIndexColumns = await GetClusteredIndexColumnsAsync(
                            ctx,
                            groupTableName.Schema,
                            groupTableName.Name,
                            request.Transaction,
                            mappings,
                            cancellationToken).ConfigureAwait(false);

                        sortedEntities.AddRange(clusteredIndexColumns.Any()
                            ? Sort(groupEntities, clusteredIndexColumns)
                            : groupEntities);
                    }

                    request.Entities = sortedEntities;
                    s0.Stop();
                    response.TimeElapsedDuringSorting = s0.Elapsed;
                }

                await DoBulkInsertAllAsync(
                    ctx,
                    request.Entities.Cast<dynamic>().ToList(),
                    request.Transaction,
                    request.EnableRecursiveInsert,
                    request.AllowNotNullSelfReferences,
                    request.CommandTimeout,
                    new Dictionary<object, object>(new IdentityEqualityComparer<object>()),
                    mappingsByType,
                    response,
                    request.UseTableLock,
                    cancellationToken).ConfigureAwait(false);

                if (request.UpdateStatistics)
                {
                    // TPH subtypes share a table; only update each distinct table once.
                    var elapsed = TimeSpan.Zero;
                    var updatedTables = new HashSet<string>();
                    foreach (var typeGroup in typeGroups)
                    {
                        var groupTableName = extractor.GetTableName(ctx, typeGroup.Key);
                        if (!updatedTables.Add($"{groupTableName.Schema}.{groupTableName.Name}"))
                            continue;

                        elapsed += await UpdateStatisticsCoreAsync(
                            ctx,
                            groupTableName,
                            request.Transaction,
                            request.CommandTimeout,
                            cancellationToken).ConfigureAwait(false);
                    }

                    response.TimeElapsedDuringUpdateStatistics = elapsed;
                }

                succeeded = true;
            }
            finally
            {
                // Re-enable even if the caller token is cancelled / insert left bad rows.
                // Every table is attempted; re-enable failures only surface when the
                // insert itself succeeded, so the original exception is never masked.
                await ReenableAllCheckConstraintsAsync(
                    response.TablesWithNoCheckConstraints,
                    tableName => ReenableCheckConstraintsAsync(ctx, tableName, request.Transaction),
                    throwOnFailure: succeeded).ConfigureAwait(false);
            }

            s.Stop();
            response.Elapsed = s.Elapsed;

            return response;
        }

        /// <summary>
        /// Runs UPDATE STATISTICS … WITH ALL on the mapped table for <typeparamref name="T"/>.
        /// </summary>
        public static BulkInsertResponse UpdateStatistics<T>(this DbContext ctx)
        {
            return UpdateStatisticsAsync<T>(ctx).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="UpdateStatistics{T}(DbContext)"/>
        public static Task<BulkInsertResponse> UpdateStatisticsAsync<T>(
            this DbContext ctx,
            CancellationToken cancellationToken = default)
        {
            return UpdateStatisticsAsync<T>(ctx, TimeSpan.FromMinutes(15), cancellationToken);
        }

        /// <summary>
        /// Runs UPDATE STATISTICS … WITH ALL on the mapped table for <typeparamref name="T"/>.
        /// </summary>
        /// <param name="timeout">Command timeout for the UPDATE STATISTICS statement.</param>
        public static BulkInsertResponse UpdateStatistics<T>(this DbContext ctx, TimeSpan timeout)
        {
            return UpdateStatisticsAsync<T>(ctx, timeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc cref="UpdateStatistics{T}(DbContext, TimeSpan)"/>
        public static async Task<BulkInsertResponse> UpdateStatisticsAsync<T>(
            this DbContext ctx,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            var response = new BulkInsertResponse();
            var tableName = GetMappingExtractor(ctx).GetTableName(ctx, typeof(T));
            response.TimeElapsedDuringUpdateStatistics = await UpdateStatisticsCoreAsync(
                ctx,
                tableName,
                transaction: null,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return response;
        }

        #endregion
    }
}
