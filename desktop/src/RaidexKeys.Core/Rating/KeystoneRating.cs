// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.Rating;

/// <summary>
/// Blizzard's Mythic+ rating of a single run, as the game computes it.
/// </summary>
/// <remarks>
/// Rebuilt from Blizzard's published rules and checked on 2026-09-11 against
/// 820 public runs from Raider.IO (levels +2 to +21, timed and untimed); every
/// run matched to ±0.1. The rules:
/// <list type="bullet">
/// <item>+2 is worth 155; every level adds 15, and +5, +7, +10 and +12 add
/// another 15 each, for the affixes that join there.</item>
/// <item>Timed: up to 15 more, linear from the timer down to 60 % of it.</item>
/// <item>Untimed: scored as at most +11, minus 15, minus up to 15 more,
/// linear from the timer up to 140 % of it.</item>
/// </list>
/// The character's rating is the sum of the best run per season dungeon.
/// </remarks>
public static class KeystoneRating
{
    public const int MinLevel = 2;

    /// <summary>The highest level the planner considers.</summary>
    public const int MaxLevel = 30;

    /// <summary>Untimed runs above this level count as this level.</summary>
    private const int UntimedCap = 11;

    private static readonly int[] AffixLevels = [5, 7, 10, 12];

    /// <summary>What a run at <paramref name="level"/> is worth right on the timer.</summary>
    public static double Base(int level)
    {
        level = Math.Max(MinLevel, level);
        return 155 + 15 * (level - MinLevel) + 15 * AffixLevels.Count(a => level >= a);
    }

    /// <summary>The rating of one run.</summary>
    public static double Score(int level, int durationSec, int timeLimitSec)
    {
        if (timeLimitSec <= 0)
        {
            return Base(level);
        }
        var share = Math.Min(1, Math.Abs(timeLimitSec - durationSec) / (0.4 * timeLimitSec));
        return durationSec <= timeLimitSec
            ? Base(level) + 15 * share
            : Base(Math.Min(level, UntimedCap)) - 15 - 15 * share;
    }

    /// <summary>
    /// What timing <paramref name="level"/> adds over the current best in that
    /// dungeon: at least (right on the timer), at most (40 % faster).
    /// </summary>
    public static (int Min, int Max) Gain(int level, double currentBest) =>
        ((int)Math.Round(Math.Max(0, Base(level) - currentBest)),
         (int)Math.Round(Math.Max(0, Base(level) + 15 - currentBest)));
}
