/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore
{
    public static partial class DbContextExtensions
    {

        /// <summary>
        /// Stages entities in a temp table, UPDATEs the target on key match, and
        /// when InsertIfNew is set INSERTs rows present in temp but not the target
        /// (EXCEPT anti-join). Concurrency tokens (issue #34) are included in the
        /// UPDATE join; a mismatch throws <see cref="DbUpdateConcurrencyException"/>.
        /// </summary>
        private static async Task DoBulkUpdateAllAsync(
            this DbContext ctx,
            BulkUpdateRequest request,
            BulkOperationResponse response,
            CancellationToken cancellationToken = default)
        {
            var rowsAffected = 0;

            var entities = request.Entities;
            var callerTransaction = request.Transaction;
            var transaction = callerTransaction;

            Type t = request.Entities[0].GetType();
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var concurrencyTokenMappings = mappings.ConcurrencyTokenMappings ?? Array.Empty<TableColumnMapping>();
            var keyPropertyNames = request.KeyPropertyNames;
            var updatedPropertyNames = request.UpdatedPropertyNames;
            var keyColumnNames = keyPropertyNames.Select(n=>columnMappings[n].TableColumn.Column.Name).ToArray();
            var updatedColumnNames = updatedPropertyNames.Select(n=>columnMappings[n].TableColumn.Column.Name).ToArray();
            
            //
            // Check to see if the table has a primary key. If so,
            // get a clr property name to table column name mapping.
            //
            var primaryKeyMembers = GetPrimaryKeyMembers(columnMappings);

            var selectedKeyMembers = keyColumnNames.Any() ? keyColumnNames : primaryKeyMembers.ToArray();
            var allKeyMembers = new List<string>();
            allKeyMembers.AddRange(primaryKeyMembers);
            allKeyMembers.AddRange(keyColumnNames);

            var selectedKeyMappings = columnMappings.Values
                .Where(m => selectedKeyMembers.Contains(m.TableColumn.Column.Name))
                .ToArray();

            if (selectedKeyMappings.Any())
            {
                //
                // Get a clr property name to table column name mapping
                // for the columns we want to update. Exclude any primary
                // key column as well as any column that we chose to use
                // as a key column in this specific update operation.
                //
                var modifiedColumnMappingCandidates = columnMappings.Values
                    .Where(m => !allKeyMembers.Contains(m.TableColumn.Column.Name))
                    .Select(m => m)
                    .ToArray();
                if (updatedColumnNames.Any())
                {
                    modifiedColumnMappingCandidates = modifiedColumnMappingCandidates
                        .Where(c => updatedColumnNames.Contains(c.TableColumn.Column.Name)).ToArray();
                }

                // Never SET concurrency tokens; SQL Server updates rowversion itself.
                var concurrencyColumnNames = new HashSet<string>(
                    concurrencyTokenMappings.Select(m => m.TableColumn.Column.Name),
                    StringComparer.OrdinalIgnoreCase);
                var modifiedColumnMappings = modifiedColumnMappingCandidates
                    .Where(c => !concurrencyColumnNames.Contains(c.TableColumn.Column.Name))
                    .ToArray();

                if (modifiedColumnMappings.Length == 0)
                {
                    throw new ArgumentException(
                        "BulkUpdate requires at least one updatable column. " +
                        "UpdatedPropertyNames must include a non-key, non-concurrency-token mapped property, " +
                        "or the entity must have such columns when UpdatedPropertyNames is empty.");
                }

                //
                // Create and populate a temp table to hold the updated values.
                //
                var conn = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);

                SqlTransaction ownedTransaction = null;
                if (concurrencyTokenMappings.Length > 0 && transaction == null)
                {
                    ownedTransaction = conn.BeginTransaction();
                    SqlTransactionTracker.NotifyCreated();
                    transaction = ownedTransaction;
                }

                string tempTableName = null;
                try
                {
                    tempTableName = await FillTempTableAsync(
                        conn,
                        entities,
                        tableName,
                        columnMappings,
                        selectedKeyMappings,
                        modifiedColumnMappings,
                        transaction,
                        request.CommandTimeout,
                        request.UseTableLock,
                        cancellationToken,
                        concurrencyTokenMappings).ConfigureAwait(false);

                    //
                    // Update the target table using the temp table we just created.
                    //
                    var setStatements =
                        modifiedColumnMappings.Select(c => $"t0.[{c.TableColumn.Column.Name}] = t1.[{c.TableColumn.Column.Name}]");
                    var setStatementsSql = string.Join(" , ", setStatements);
                    var keyConditionStatements =
                        selectedKeyMappings.Select(c => $"t0.[{c.TableColumn.Column.Name}] = t1.[{c.TableColumn.Column.Name}]");
                    var updateConditionStatements = keyConditionStatements
                        .Concat(concurrencyTokenMappings.Select(c =>
                            $"t0.[{c.TableColumn.Column.Name}] = t1.[{c.TableColumn.Column.Name}]"));
                    var updateConditionStatementsSql = string.Join(" AND ", updateConditionStatements);
                    var keyConditionStatementsSql = string.Join(" AND ", keyConditionStatements);
                    var cmdBody = $@"UPDATE t0 SET {setStatementsSql}
                                     FROM {tableName.Fullname} AS t0
                                     INNER JOIN {tempTableName} AS t1 ON {updateConditionStatementsSql}
                                    ";
                    using (var cmd = CreateSqlCommand(cmdBody, conn, transaction, request.CommandTimeout))
                    {
                        rowsAffected += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (request.InsertIfNew)
                    {
                        // Include client-assigned PKs; omit store-generated columns
                        // (IDENTITY, computed, rowversion) so SQL Server supplies them.
                        // Matching existing rows still uses keyCondition only (not tokens).
                        var columns = columnMappings.Values
                            .Where(m => !m.IsStoreGenerated)
                            .Where(m => !concurrencyColumnNames.Contains(m.TableColumn.Column.Name))
                            .Select(m => m.TableColumn.Column.Name)
                            .ToArray();
                        var columnNames = string.Join(",", columns.Select(c => $"[{c}]"));
                        var t0ColumnNames = string.Join(",", columns.Select(c => $"[t0].[{c}]"));
                        cmdBody = $@"INSERT INTO {tableName.Fullname}
                                 SELECT {columnNames}
                                 FROM {tempTableName}
                                 EXCEPT
                                 SELECT {t0ColumnNames}
                                 FROM {tempTableName} AS t0
                                 INNER JOIN {tableName.Fullname} AS t1 ON {keyConditionStatementsSql}            
                                ";
                        using (var cmd = CreateSqlCommand(cmdBody, conn, transaction, request.CommandTimeout))
                        {
                            rowsAffected += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (concurrencyTokenMappings.Length > 0 && rowsAffected != entities.Count)
                    {
                        ownedTransaction?.Rollback();
                        throw new DbUpdateConcurrencyException(
                            $"BulkUpdate expected to affect {entities.Count} row(s) but affected {rowsAffected}. " +
                            "One or more entities may have been modified or deleted (optimistic concurrency).");
                    }

                    ownedTransaction?.Commit();
                }
                catch
                {
                    try { ownedTransaction?.Rollback(); } catch { /* ignore */ }
                    throw;
                }
                finally
                {
                    // Dispose owned concurrency txn first. Drop with the caller
                    // transaction only (null when we owned it)—never a disposed
                    // SqlTransaction.
                    if (ownedTransaction != null)
                    {
                        ownedTransaction.Dispose();
                        SqlTransactionTracker.NotifyDisposed();
                    }
                    if (tempTableName != null)
                        await DropTempTableAsync(conn, callerTransaction, tempTableName, CancellationToken.None).ConfigureAwait(false);
                }
            }

            response.AffectedRows.Add(new Tuple<Type, long>(t, rowsAffected));
        }
    }
}
