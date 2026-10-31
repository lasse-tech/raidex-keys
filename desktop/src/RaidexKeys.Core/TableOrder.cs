// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core;

/// <summary>How a table orders its rows by one column.</summary>
public static class TableOrder
{
    /// <summary>
    /// <paramref name="rows"/> by <paramref name="key"/>. Rows without a value
    /// (a character without a key, a dungeon not run yet) go to the end in
    /// either direction; rows with equal values keep their order. Text
    /// compares as the reader's language sorts it.
    /// </summary>
    public static List<T> Sort<T>(IEnumerable<T> rows, Func<T, IComparable?> key, bool descending)
    {
        var keyed = rows.Select(row => (Row: row, Key: key(row))).ToList();
        var comparer = Comparer<IComparable>.Create(Compare);
        var valued = keyed.Where(k => k.Key is not null);
        var sorted = descending
            ? valued.OrderByDescending(k => k.Key!, comparer)
            : valued.OrderBy(k => k.Key!, comparer);
        return sorted.Concat(keyed.Where(k => k.Key is null)).Select(k => k.Row).ToList();
    }

    private static int Compare(IComparable? a, IComparable? b) => a is string x && b is string y
        ? StringComparer.CurrentCulture.Compare(x, y)
        : a!.CompareTo(b);
}
