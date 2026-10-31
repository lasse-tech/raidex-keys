// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;

namespace RaidexKeys.Core;

/// <summary>A keystone as a character carries it.</summary>
public sealed record Keystone(int MapId, string Dungeon, int Level)
{
    public string LevelLabel => $"+{Level}";
}

/// <summary>A route from Mythic Dungeon Tools, reduced to the next run's line.</summary>
public sealed record RouteSummary(string Dungeon, string Name, double ForcesPercent, int Pulls)
{
    /// <summary>Enemy forces with two decimals as MDT shows them, in the reader's number format: "101,43 %" or "101.43%".</summary>
    public string ForcesLabel => (ForcesPercent / 100).ToString("P2", CultureInfo.CurrentCulture);
}
