// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.Tests;

/// <summary>A table sorted by a column: rows without a value last, ties in their order.</summary>
public sealed class TableOrderTests
{
    private sealed record Row(string Name, int? Level);

    private static readonly Row[] Rows = [new("a", 10), new("b", null), new("c", 12), new("d", 10), new("e", null)];

    [Fact]
    public void EmptyValuesGoLastEitherWayTiesKeepTheirOrder()
    {
        Assert.Equal("adcbe", string.Concat(TableOrder.Sort(Rows, r => r.Level, descending: false).Select(r => r.Name)));
        Assert.Equal("cadbe", string.Concat(TableOrder.Sort(Rows, r => r.Level, descending: true).Select(r => r.Name)));
    }
}
