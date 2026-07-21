using System;
using System.Collections.Generic;

namespace Hollowfen.UI
{
    public static class JournalNavigation
    {
        public static int FindIndex<T>(IReadOnlyList<T> items, T current)
        {
            if (items == null) return -1;
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < items.Count; i++)
                if (comparer.Equals(items[i], current)) return i;
            return -1;
        }

        public static int FindAdjacentAvailable<T>(
            IReadOnlyList<T> items,
            int fromIndex,
            int direction,
            Predicate<T> isAvailable)
        {
            if (items == null || direction == 0) return -1;
            int step = direction < 0 ? -1 : 1;
            for (int i = fromIndex + step; i >= 0 && i < items.Count; i += step)
            {
                T item = items[i];
                if (item != null && (isAvailable == null || isAvailable(item))) return i;
            }
            return -1;
        }

        public static int CountAvailable<T>(IReadOnlyList<T> items, Predicate<T> isAvailable)
        {
            if (items == null) return 0;
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                T item = items[i];
                if (item != null && (isAvailable == null || isAvailable(item))) count++;
            }
            return count;
        }

        public static int AvailablePosition<T>(
            IReadOnlyList<T> items,
            int currentIndex,
            Predicate<T> isAvailable)
        {
            if (items == null || currentIndex < 0) return -1;
            int position = 0;
            for (int i = 0; i <= currentIndex && i < items.Count; i++)
            {
                T item = items[i];
                if (item != null && (isAvailable == null || isAvailable(item))) position++;
            }
            return position;
        }
    }
}
