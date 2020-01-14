using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Tanneryd.BulkOperations.EF6.NetStd
{
    public static class ListExtensions
    {
        public static T[] ConvertToArray<T>(IList list)
        {
            return list.Cast<T>().ToArray();
        }

        public static object[] ToArray(this IList list, Type elementType)
        {
            var convertMethod = typeof(ListExtensions).GetMethod("ConvertToArray",
                BindingFlags.Static | BindingFlags.Public, null, new[] {typeof(IList)}, null);
            var genericMethod = convertMethod.MakeGenericMethod(elementType);
            return (object[]) genericMethod.Invoke(null, new object[] {list});
        }
    }
}