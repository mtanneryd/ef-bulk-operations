/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
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
            var mappings = MappingExtractor.GetMappings(ctx, t);
            var tableName = mappings.TableName;
            var columnMappings = mappings.ColumnMappingByPropertyName;
            var keyPropertyNames = request.KeyPropertyNames;
            var updatedPropertyNames = request.UpdatedPropertyNames;
            var keyColumnNames = keyPropertyNames.Select(n=>columnMappings[n].TableColumn.Name).ToArray();
            var updatedColumnNames = updatedPropertyNames.Select(n=>columnMappings[n].TableColumn.Name).ToArray();

            var primaryKeyMembers = GetPrimaryKeyMembers(columnMappings);

            var selectedKeyMembers = keyColumnNames.Any() ? keyColumnNames : primaryKeyMembers.ToArray();
            var allKeyMembers = new List<string>();
            allKeyMembers.AddRange(primaryKeyMembers);
            allKeyMembers.AddRange(keyColumnNames);

            var selectedKeyMappings = columnMappings.Values
                .Where(m => selectedKeyMembers.Contains(m.TableColumn.Name))
                .ToArray();

            if (selectedKeyMappings.Any())
            {
                var modifiedColumnMappingCandidates = columnMappings.Values
                    .Where(m => !allKeyMembers.Contains(m.TableColumn.Name))
                    .Select(m => m)
                    .ToArray();
                if (updatedColumnNames.Any())
                {
                    modifiedColumnMappingCandidates = modifiedColumnMappingCandidates
                        .Where(c => updatedColumnNames.Contains(c.TableColumn.Name)).ToArray();
                }

                var modifiedColumnMappings = modifiedColumnMappingCandidates.ToArray();

                var conn = await ResolveSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
                var tempTableName = await FillTempTableAsync(
                    conn,
                    entities,
                    tableName,
                    columnMappings,
                    selectedKeyMappings,
                    modifiedColumnMappings,
                    transaction,
                    cancellationToken).ConfigureAwait(false);

                var setStatements =
                    modifiedColumnMappings.Select(c => $"t0.[{c.TableColumn.Name}] = t1.[{c.TableColumn.Name}]");
                var setStatementsSql = string.Join(" , ", setStatements);
                var conditionStatements =
                    selectedKeyMappings.Select(c => $"t0.[{c.TableColumn.Name}] = t1.[{c.TableColumn.Name}]");
                var conditionStatementsSql = string.Join(" AND ", conditionStatements);
                var cmdBody = $@"UPDATE t0 SET {setStatementsSql}
                                 FROM {tableName.Fullname} AS t0
                                 INNER JOIN {tempTableName} AS t1 ON {conditionStatementsSql}
                                ";
                var cmd = CreateSqlCommand(cmdBody, conn, request.Transaction, request.CommandTimeout);
                rowsAffected += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                if (request.InsertIfNew)
                {
                    var columns = columnMappings.Values
                        .Where(m => !primaryKeyMembers.Contains(m.TableColumn.Name))
                        .Select(m => m.TableColumn.Name)
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
                    cmd = CreateSqlCommand(cmdBody, conn, request.Transaction, request.CommandTimeout);
                    rowsAffected += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await DropTempTableAsync(conn, transaction, tempTableName, cancellationToken).ConfigureAwait(false);
            }

            response.AffectedRows.Add(new Tuple<Type, long>(t, rowsAffected));
        }
    }
}
