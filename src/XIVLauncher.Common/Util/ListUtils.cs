using System.Collections.Generic;

namespace XIVLauncher.Common.Util;

public static class ListUtils
{
    /// <summary>
    /// Perform a "zipper merge" (A, 1, B, 2, C, 3) of multiple enumerables, allowing for lists to end early.
    /// </summary>
    /// <param name="sources">A set of enumerable sources to combine.</param>
    /// <typeparam name="TSource">The resulting type of the merged list to return.</typeparam>
    /// <returns>A new enumerable, consisting of the final merge of all lists.</returns>
    public static IEnumerable<TSource> ZipperMerge<TSource>(params IEnumerable<TSource>[] sources)
    {
        // Borrowed from https://codereview.stackexchange.com/a/263451, thank you!
        var enumerators = new IEnumerator<TSource>[sources.Length];

        try
        {
            for (var i = 0; i < sources.Length; i++)
            {
                enumerators[i] = sources[i].GetEnumerator();
            }

            var hasNext = new bool[enumerators.Length];

            bool MoveNext()
            {
                var anyHasNext = false;

                for (var i = 0; i < enumerators.Length; i++)
                {
                    anyHasNext |= hasNext[i] = enumerators[i].MoveNext();
                }

                return anyHasNext;
            }

            while (MoveNext())
            {
                for (var i = 0; i < enumerators.Length; i++)
                {
                    if (hasNext[i])
                    {
                        yield return enumerators[i].Current;
                    }
                }
            }
        }
        finally
        {
            foreach (var enumerator in enumerators)
            {
                enumerator.Dispose();
            }
        }
    }
}
