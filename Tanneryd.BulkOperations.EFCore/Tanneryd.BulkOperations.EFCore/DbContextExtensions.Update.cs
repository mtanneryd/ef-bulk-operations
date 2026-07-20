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
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore
{
    public static partial class DbContextExtensions
    {
        private static void DoBulkUpdateAll(
            this DbContext ctx,
            BulkUpdateRequest request,
            BulkOperationResponse response)
        {
            DoBulkUpdateAllAsync(ctx, request, response).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Stages entities in a temp table, UPDATEs the target on key match, and
        /// when InsertIfNew is set INSERTs rows present in temp but not the target
        /// (EXCEPT anti-join).
        /// </summary>
        private static async Task DoBulkUpdateAllAsync(
            this DbContext ctx,
            BulkUpdateRequest request,
            BulkOperationResponse response,
            CancellationToken cancellationToken = default)
        {
            var rowsAffected = 0;

            var entities = request.Entities;
            var transaction = request.Transaction;

            Type t = request.Entities[0].GetType();
            var mappings = GetMappingExtractor(ctx).GetMappings(t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
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

                var modifiedColumnMappings = modifiedColumnMappingCandidates.ToArray();

                //
                // Create and populate a temp table to hold the updated values.
                //
                var conn = await GetSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
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
                        cancellationToken).ConfigureAwait(false);

                    //
                    // Update the target table using the temp table we just created.
                    //
                    var setStatements =
                        modifiedColumnMappings.Select(c => $"t0.[{c.TableColumn.Column.Name}] = t1.[{c.TableColumn.Column.Name}]");
                    var setStatementsSql = string.Join(" , ", setStatements);
                    var conditionStatements =
                        selectedKeyMappings.Select(c => $"t0.[{c.TableColumn.Column.Name}] = t1.[{c.TableColumn.Column.Name}]");
                    var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                    var cmdBody = $@"UPDATE t0 SET {setStatementsSql}
                                     FROM {tableName.Fullname} AS t0
                                     INNER JOIN {tempTableName} AS t1 ON {conditionStatementsSql}
                                    ";
                    using (var cmd = CreateSqlCommand(cmdBody, conn, request.Transaction, request.CommandTimeout))
                    {
                        rowsAffected += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    if (request.InsertIfNew)
                    {
                        var columns = columnMappings.Values
                            .Where(m => !primaryKeyMembers.Contains(m.TableColumn.Column.Name))
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
                                 INNER JOIN {tableName.Fullname} AS t1 ON {conditionStatementsSql}            
                                ";
                        using (var cmd = CreateSqlCommand(cmdBody, conn, request.Transaction, request.CommandTimeout))
                        {
                            rowsAffected += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                finally
                {
                    if (tempTableName != null)
                        await DropTempTableAsync(conn, transaction, tempTableName, CancellationToken.None).ConfigureAwait(false);
                }
            }

            response.AffectedRows.Add(new Tuple<Type, long>(t, rowsAffected));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="entities"></param>
        /// <param name="sqlTransaction"></param>
        /// <param name="recursive"></param>
        /// <param name="allowNotNullSelfReferences"></param>
        /// <param name="commandTimeout"></param>
        /// <param name="savedEntities"></param>
        /// <param name="mappingsByType"></param>
    }
}
