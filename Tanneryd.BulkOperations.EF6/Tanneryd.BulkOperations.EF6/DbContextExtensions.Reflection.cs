/*
 * Copyright ©  2017-2026 Tånneryd IT AB
 * Licensed under the Apache License, Version 2.0.
 */

using Microsoft.Data.SqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Tanneryd.BulkOperations.EF6.Model;

namespace Tanneryd.BulkOperations.EF6
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
        /// When every row is null for a key, falls back to
        /// <paramref name="columnMappings"/> so all-null columns are not omitted.
        /// </summary>
        private static BulkPropertyInfo[] GetProperties(
            IList entities,
            IDictionary<string, TableColumnMapping> columnMappings = null)
        {
            if (entities == null || entities.Count == 0)
                return Array.Empty<BulkPropertyInfo>();

            if (!(entities[0] is ExpandoObject))
                return GetProperties(entities[0]);

            var typeByName = new Dictionary<string, Type>(StringComparer.Ordinal);
            var keysSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entity in entities)
            {
                var dict = (IDictionary<string, object>)(ExpandoObject)entity;
                foreach (var kvp in dict)
                {
                    keysSeen.Add(kvp.Key);
                    if (kvp.Value == null || typeByName.ContainsKey(kvp.Key))
                        continue;
                    typeByName[kvp.Key] = kvp.Value.GetType();
                }
            }

            // Value-only inference drops keys that are null on every row. Prefer
            // mapping-declared CLR types for those (complex-type flatten / M2M join).
            if (columnMappings != null)
            {
                foreach (var key in keysSeen)
                {
                    if (typeByName.ContainsKey(key))
                        continue;
                    if (!columnMappings.TryGetValue(key, out var mapping))
                        continue;
                    var mappedType = mapping.EntityProperty?.PrimitiveType?.ClrEquivalentType;
                    if (mappedType != null)
                        typeByName[key] = mappedType;
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

        private static Task<SqlServerConnection> ResolveSqlConnectionAsync(
            DbContext ctx,
            CancellationToken cancellationToken = default)
        {
            return SqlServerConnection.ResolveAsync(ctx, cancellationToken);
        }

        public static SqlConnection GetSqlConnection(this DbContext ctx)
        {
            return GetSqlConnectionAsync(ctx).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns the underlying Microsoft.Data.SqlClient connection for the context.
        /// </summary>
        /// <remarks>
        /// Prefer this async overload from async call sites. The sync
        /// <see cref="GetSqlConnection"/> method blocks with
        /// ConfigureAwait(false).GetAwaiter().GetResult().
        /// </remarks>
        public static async Task<SqlConnection> GetSqlConnectionAsync(
            this DbContext ctx,
            CancellationToken cancellationToken = default)
        {
            var connection = await ResolveSqlConnectionAsync(ctx, cancellationToken).ConfigureAwait(false);
            await connection.EnsureOpenAsync(cancellationToken).ConfigureAwait(false);
            return connection.AsModernConnection();
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
            var isGuid = p.TypeName == "Guid" || p.TypeName == "uniqueidentifier";
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
        /// True when a key/FK value is considered unset (null, empty Guid,
        /// default DateTime, empty string, or numeric zero). Avoids dynamic
        /// comparisons like <c>value == 0</c> that throw for Guid/string.
        /// </summary>
        private static bool IsUnsetKeyValue(object value, Type clrType)
        {
            if (value == null || value == DBNull.Value)
                return true;

            clrType = Nullable.GetUnderlyingType(clrType) ?? clrType;

            if (clrType == typeof(Guid))
                return value is Guid g && g == Guid.Empty;
            if (clrType == typeof(DateTime))
                return value is DateTime dt && dt == default;
            if (clrType == typeof(string))
                return string.IsNullOrEmpty(value as string);
            if (clrType == typeof(bool))
                return false;

            switch (value)
            {
                case byte b: return b == 0;
                case sbyte sb: return sb == 0;
                case short s: return s == 0;
                case ushort us: return us == 0;
                case int i: return i == 0;
                case uint ui: return ui == 0;
                case long l: return l == 0;
                case ulong ul: return ul == 0;
                case float f: return f == 0;
                case double d: return d == 0;
                case decimal m: return m == 0;
            }

            if (clrType.IsValueType)
                return Equals(value, Activator.CreateInstance(clrType));

            return false;
        }

        private static bool IsKeyValueSet(object value, Type clrType) =>
            !IsUnsetKeyValue(value, clrType);

        private static object CreateUnsetKeyValue(Type clrType)
        {
            clrType = Nullable.GetUnderlyingType(clrType) ?? clrType;
            if (!clrType.IsValueType)
                return null;
            return Activator.CreateInstance(clrType);
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

        private static SqlServerCommand CreateSqlCommand(
            string query,
            SqlServerConnection connection,
            SqlTransaction transaction,
            TimeSpan timeout)
        {
            return connection.CreateCommand(query, transaction, timeout);
        }
    }
}
