/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.Common.Sql;
using Tanneryd.BulkOperations.EFCore.Model;

namespace Tanneryd.BulkOperations.EFCore
{
    public static partial class DbContextExtensions
    {
        private static BulkPropertyInfo[] GetProperties(Type t)
        {
            var bulkProperties = t.GetProperties().Select(p => new RegularBulkPropertyInfo
            {
                PropertyInfo = p
            }).ToArray();

            return bulkProperties.Cast<BulkPropertyInfo>().ToArray();
        }

        /// <summary>
        /// Builds the bulk-copy property list for a batch. For <see cref="ExpandoObject"/>
        /// rows, unions keys across all entities and takes each property's CLR type from
        /// the first non-null value so a null on entities[0] does not drop the column.
        /// </summary>
        private static BulkPropertyInfo[] GetProperties(IList entities)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<BulkPropertyInfo>();

            if (entities[0] is not ExpandoObject)
                return GetProperties(entities[0]);

            var typeByName = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var entity in entities)
            {
                var dict = (IDictionary<string, object>)(ExpandoObject)entity;
                foreach (var kvp in dict)
                {
                    if (kvp.Value == null || typeByName.ContainsKey(kvp.Key))
                        continue;
                    typeByName[kvp.Key] = kvp.Value.GetType();
                }
            }

            return typeByName
                .Select(kvp => (BulkPropertyInfo)new ExpandoBulkPropertyInfo
                {
                    Name = kvp.Key,
                    Type = kvp.Value
                })
                .ToArray();
        }

        private static BulkPropertyInfo[] GetProperties(object o)
        {
            if (o is ExpandoObject)
            {
                // Prefer GetProperties(IList) for batches. Single-row path still
                // skips nulls (no type to infer); callers that need null columns
                // must supply a row with a non-null sample or use the IList overload.
                var props = new List<ExpandoBulkPropertyInfo>();
                var dict = (IDictionary<string, object>)o;

                foreach (var kvp in dict.Where(kvp => kvp.Value != null))
                {
                    props.Add(new ExpandoBulkPropertyInfo
                    {
                        Name = kvp.Key,
                        Type = kvp.Value.GetType()
                    });
                }

                return props.Cast<BulkPropertyInfo>().ToArray();
            }

            Type t = o.GetType();
            var bulkProperties = t.GetProperties().Select(p => new RegularBulkPropertyInfo
            {
                PropertyInfo = p
            }).ToArray();

            return bulkProperties.Cast<BulkPropertyInfo>().ToArray();
        }

        private static void AddProperty(ExpandoObject expando, string propertyName, object propertyValue)
        {
            // ExpandoObject supports IDictionary so we can extend it like this
            var expandoDict = expando as IDictionary<string, object>;
            if (expandoDict.ContainsKey(propertyName))
                expandoDict[propertyName] = propertyValue;
            else
                expandoDict.Add(propertyName, propertyValue);
        }

        public static SqlConnection GetSqlConnection(this DbContext ctx)
        {
            return GetSqlConnectionAsync(ctx).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static async Task<SqlConnection> GetSqlConnectionAsync(
            this DbContext ctx,
            CancellationToken cancellationToken = default)
        {
            var conn = (SqlConnection)ctx.Database.GetDbConnection();
            if (conn.State == ConnectionState.Closed)
                await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            return conn;
        }


        /// <summary>
        /// Use reflection to get the property value by its property 
        /// name from an object instance.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="instance"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        private static dynamic GetProperty(string propertyName, object instance, object def = null)
        {
            var t = instance.GetType();
            return GetProperty(t, propertyName, instance, def);
        }

        private static dynamic GetProperty(Type t, string propertyName, object instance, object def = null)
        {
            if (t.IsPrimitive) return instance;
            if (t == typeof(string)) return instance;

            var property = t.GetProperty(propertyName);
            return GetProperty(property, instance, def);
        }

        private static dynamic GetProperty(string propertyName, ExpandoObject instance)
        {
            var dict = (IDictionary<string, object>)instance;
            if (!dict.TryGetValue(propertyName, out object value) || value == null)
                return DBNull.Value;
            return value;
        }

        private static dynamic GetProperty(PropertyInfo property, object instance, object def = null)
        {
            var val = property.GetValue(instance);
            return val ?? def;
        }

        private static bool IsGuidProperty(dynamic p)
        {
            var isGuid = p.TypeMapping.ClrType == typeof(System.Guid);
            return isGuid;
        }

        private static bool IsGuid(Type t)
        {
            var isGuid = (t == typeof(Guid) || t == typeof(Guid?));
            return isGuid;
        }

        private static bool IsDateTime(Type t)
        {
            var isDateTime = (t == typeof(DateTime) || t == typeof(DateTime?));
            return isDateTime;
        }

        /// <summary>
        /// Use reflection to set a property value by its property 
        /// name to an object instance.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="instance"></param>
        /// <param name="value"></param>
        private static void SetProperty(string propertyName, object instance, object value)
        {
            if (value == DBNull.Value) return;

            if (instance is ExpandoObject)
            {
                var dict = (IDictionary<string, object>)instance;
                dict[propertyName] = value;
            }
            else
            {
                var type = instance.GetType();
                var property = type.GetProperty(propertyName);
                property.SetValue(instance, value);
            }
        }

        private static void SetProperty(BulkPropertyInfo property, object instance, object value)
        {
            if (value == DBNull.Value) return;

            if (property is RegularBulkPropertyInfo) property.PropertyInfo.SetValue(instance, value);
            else
            {
                var dict = (IDictionary<string, object>)instance;
                dict[property.Name] = value;
            }
        }

        private static SqlCommand CreateSqlCommand(
            string query,
            SqlConnection connection,
            SqlTransaction transaction,
            TimeSpan timeout)
        {
            return SqlCommandFactory.Create(query, connection, transaction, timeout);
        }
    }
}
