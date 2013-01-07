using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy.Extensions
{
    internal static class EmptyExtensions
    {
        public static bool IsNullOrEmpty(this String val)
        {
            if (val == null)
                return true;

            if (val.Length == 0)
                return true;

            return false;
        }

        public static bool IsNullOrEmpty<T>(this IList<T> list)
        {
            if (list == null || list.Count < 1)
                return true;

            return false;
        }

        public static bool IsNotNullAndNotEmpty<T>(this IList<T> list)
        {
            if (list != null && list.Count > 0)
                return true;

            return false;
        }

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> col)
        {
            if (col == null || col.Count() < 1)
                return true;

            return false;
        }

        public static bool IsNotNullAndNotEmpty<T>(this IEnumerable<T> col)
        {
            if (col != null && col.Count() > 0)
                return true;

            return false;
        }
    }
}
