using System;
using System.Collections.Generic;

namespace GoldenHour
{
    /// <summary>List-style helpers for read-only lists exposed by the director.</summary>
    public static class ReadOnlyListExtensions
    {
        public static T Find<T>(this IReadOnlyList<T> list, Predicate<T> match) where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (match(list[i])) return list[i];
            }

            return null;
        }

        public static List<T> FindAll<T>(this IReadOnlyList<T> list, Predicate<T> match)
        {
            List<T> result = new List<T>();
            for (int i = 0; i < list.Count; i++)
            {
                if (match(list[i])) result.Add(list[i]);
            }

            return result;
        }

        public static bool TrueForAll<T>(this IReadOnlyList<T> list, Predicate<T> match)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!match(list[i])) return false;
            }

            return true;
        }
    }
}
