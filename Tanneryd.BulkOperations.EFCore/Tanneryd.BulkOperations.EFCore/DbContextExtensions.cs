/*
 * Copyright ©  2017-2020 Tånneryd IT AB
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

        private static readonly object _mutex = new object();
        private static readonly Dictionary<Type, MappingsExtractor> _mappingExtractorsByContextType =
            new Dictionary<Type, MappingsExtractor>();

        #region Public API

        public static void DeleteAllExecutionPlansFromCache(this DbContext ctx, SqlTransaction sqlTransaction)
        {
            DeleteAllExecutionPlansFromCacheAsync(ctx, sqlTransaction).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static Task DeleteAllExecutionPlansFromCacheAsync(
            this DbContext ctx,
            SqlTransaction sqlTransaction,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            var query = $@"DBCC FREEPROCCACHE WITH NO_INFOMSGS";
            return DeleteAllExecutionPlansFromCacheCoreAsync(ctx, query, sqlTransaction, cancellationToken);
        }

        private static async Task DeleteAllExecutionPlansFromCacheCoreAsync(
            DbContext ctx,
            string query,
            SqlTransaction sqlTransaction,
            CancellationToken cancellationToken)
        {
            var connection = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
            var cmd = CreateSqlCommand(query, connection, sqlTransaction, TimeSpan.FromSeconds(30));
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// The bulk delete request contains a SqlCondition. It has
        /// a list of column name/column value pairs and will be used
        /// to build an AND where clause. This method will delete any
        /// rows in the database that matches this SQL condition unless
        /// it also matches one of the supplied entities according to
        /// the key selector used.
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

        public static Task<IList<T1>> BulkSelectNotExistingAsync<T1, T2>(
            this DbContext ctx,
            BulkSelectRequest<T1> request,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            ValidateBulkSelectRequest(request);
            return DoBulkSelectNotExistingAsync<T1, T2>(ctx, request, cancellationToken);
        }

        private static IList BulkSelectNotExisting(DbContext ctx, Type t, IList entities,
            TableColumnMapping[] pkColumnMappings, SqlTransaction sqlTransaction)
        {
            var request = typeof(BulkSelectRequest<>).MakeGenericType(t);
            var keyPropertyNames = pkColumnMappings.Select(m => m.EntityProperty.Name).ToArray();
            var r = Activator.CreateInstance(request, keyPropertyNames, entities.ToArray(t), sqlTransaction);
            Type ex = typeof(DbContextExtensions);
            MethodInfo mi = ex.GetMethod("BulkSelectNotExisting");
            MethodInfo miGeneric = mi.MakeGenericMethod(new[] { t, t });
            object[] args = { ctx, r };
            var notExistingEntities = (IList)miGeneric.Invoke(null, args);

            return notExistingEntities;
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
        /// 
        /// The request object properties have the following function:
        /// 
        /// Entities - The entities are mapped to rows in a table and these table rows will be updated.
        /// UpdatedColumnNames - Specifies which columns to update. An empty list will update ALL columns.
        /// KeyMemberNames - Specifies which columns to use as row selectors. An empty list will result
        ///                  in the primary key columns to be used.
        /// Transaction - If a transaction object is provided the update will be made within that transaction.
        /// InsertIfNew - When set to true, any entities new to the table will be inserted. Otherwise they 
        ///               will be ignored.
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        public static BulkOperationResponse BulkUpdateAll(
            this DbContext ctx,
            BulkUpdateRequest request)
        {
            return BulkUpdateAllAsync(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

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
        /// Insert all entities using Microsoft.Data.SqlClient.SqlBulkCopy. 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="entities"></param>
        /// <param name="transaction"></param>
        /// <param name="recursive">True if the entire entity graph should be inserted, false otherwise.</param>
        public static BulkInsertResponse BulkInsertAll<T>(
            this DbContext ctx,
            IList<T> entities,
            SqlTransaction transaction = null,
            bool recursive = false)
        {
            return BulkInsertAllAsync(ctx, entities, transaction, recursive).ConfigureAwait(false).GetAwaiter().GetResult();
        }

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
        /// 
        /// The request object properties have the following function:
        /// 
        ///  Entities - The entities are mapped to rows in a table and these table rows will 
        ///             be updated.
        ///  Transaction - If a transaction object is provided the update will be made within that transaction.
        ///  Recursive - If true any new entities added to navigation properties will also be inserted. Foreign 
        ///              key relationships will be honored for both new and existing entities in the entire 
        ///              entity graph.
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        public static BulkInsertResponse BulkInsertAll<T>(
            this DbContext ctx,
            BulkInsertRequest<T> request)
        {
            return BulkInsertAllAsync(ctx, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

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

            try
            {
                var t = request.Entities.First().GetType();
                var tableName = GetMappingExtractor(ctx).GetTableName(ctx, t);
                var mappingsByType = new Dictionary<Type, Mappings>();
                if (request.SortUsingClusteredIndex)
                {
                    var mappings = GetMappingExtractor(ctx).GetMappings(t);
                    mappingsByType.Add(t, mappings);

                    var s0 = new Stopwatch();
                    s0.Start();
                    var clusteredIndexColumns = await GetClusteredIndexColumnsAsync(
                        ctx,
                        tableName.Schema,
                        tableName.Name,
                        request.Transaction,
                        mappings,
                        cancellationToken).ConfigureAwait(false);

                    request.Entities = clusteredIndexColumns.Any()
                        ? Sort(request.Entities, clusteredIndexColumns)
                        : request.Entities;
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
                    cancellationToken).ConfigureAwait(false);

                if (request.UpdateStatistics)
                {
                    var s0 = new Stopwatch();
                    s0.Start();
                    var query = $"UPDATE STATISTICS {tableName.Fullname} WITH ALL";
                    var connection = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
                    var cmd = CreateSqlCommand(query, connection, request.Transaction, request.CommandTimeout);
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    s0.Stop();
                    response.TimeElapsedDuringUpdateStatistics = s0.Elapsed;
                }
            }
            finally
            {
                foreach (var tableName in response.TablesWithNoCheckConstraints)
                {
                    var query = $"ALTER TABLE {tableName} WITH CHECK CHECK CONSTRAINT ALL";
                    var connection = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
                    var cmd = CreateSqlCommand(query, connection, request.Transaction, request.CommandTimeout);
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            s.Stop();
            response.Elapsed = s.Elapsed;

            return response;
        }

        public static BulkInsertResponse UpdateStatistics<T>(this DbContext ctx)
        {
            return UpdateStatisticsAsync<T>(ctx).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static Task<BulkInsertResponse> UpdateStatisticsAsync<T>(
            this DbContext ctx,
            CancellationToken cancellationToken = default)
        {
            return UpdateStatisticsAsync<T>(ctx, TimeSpan.FromMinutes(15), cancellationToken);
        }

        public static BulkInsertResponse UpdateStatistics<T>(this DbContext ctx, TimeSpan timeout)
        {
            return UpdateStatisticsAsync<T>(ctx, timeout).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static async Task<BulkInsertResponse> UpdateStatisticsAsync<T>(
            this DbContext ctx,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ValidateDbContext(ctx);
            var response = new BulkInsertResponse();
            var tableName = GetMappingExtractor(ctx).GetTableName(ctx, typeof(T));

            var s0 = new Stopwatch();
            s0.Start();
            var query = $"UPDATE STATISTICS {tableName.Fullname} WITH ALL";
            var connection = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
            var cmd = CreateSqlCommand(query, connection, null, timeout);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            s0.Stop();
            response.TimeElapsedDuringUpdateStatistics = s0.Elapsed;

            return response;
        }

        #endregion
    }
}
