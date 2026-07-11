/*
 * Copyright ©  2017-2025 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
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

            //
            // Check to see if the table has a primary key. If so,
            // get a clr property name to table column name mapping.
            //
            var primaryKeyMembers = GetPrimaryKeyMembers(columnMappings);

            var selectedKeyMembers = keyPropertyNames.Any() ? keyPropertyNames : primaryKeyMembers.ToArray();
            var allKeyMembers = new List<string>();
            allKeyMembers.AddRange(primaryKeyMembers);
            allKeyMembers.AddRange(keyPropertyNames);

            var selectedKeyMappings = columnMappings.Values
                .Where(m => selectedKeyMembers.Contains(m.TableColumn.Name))
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
                    .Where(m => !allKeyMembers.Contains(m.TableColumn.Name))
                    .Select(m => m)
                    .ToArray();
                if (updatedColumnNames.Any())
                {
                    modifiedColumnMappingCandidates = modifiedColumnMappingCandidates
                        .Where(c => updatedColumnNames.Contains(c.TableColumn.Name)).ToArray();
                }

                var modifiedColumnMappings = modifiedColumnMappingCandidates.ToArray();

                //
                // Create and populate a temp table to hold the updated values.
                //
                var conn = ResolveSqlConnection(ctx);
                var tempTableName = FillTempTable(conn, entities, tableName, columnMappings, selectedKeyMappings,
                    modifiedColumnMappings, transaction);

                //
                // Update the target table using the temp table we just created.
                //
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
                rowsAffected += cmd.ExecuteNonQuery();

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
                    rowsAffected += cmd.ExecuteNonQuery();
                }

                //
                // Clean up. Delete the temp table.
                //
                DropTempTable(conn, transaction, tempTableName);
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
