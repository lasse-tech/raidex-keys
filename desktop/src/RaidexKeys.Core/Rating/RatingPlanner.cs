// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.Rating;

/// <summary>One run of a plan: this dungeon at this level, in time.</summary>
public sealed record PlannedRun(int MapId, int Level, int Gain);

/// <summary>
/// The fewest runs that lift a rating to a target, each timed right on the
/// timer, so any time bonus comes on top.
/// </summary>
public static class RatingPlanner
{
    /// <summary>The goal the overview starts with: 100 more, rounded up to the next hundred (2648 → 2800).</summary>
    public static int DefaultTarget(int rating) => (rating + 100 + 99) / 100 * 100;

    /// <summary>The lowest goal worth setting: the next hundred above the rating.</summary>
    public static int MinTarget(int rating) => (rating / 100 + 1) * 100;

    /// <summary>The runs to reach <paramref name="target"/>, or null if even +30 everywhere falls short.</summary>
    /// <param name="bests">The best score per season dungeon; 0 for one not run yet. Needs every season dungeon.</param>
    /// <param name="levelCap">Plans stay at or below this level while they can: the highest level timed, plus what the settings allow.</param>
    public static IReadOnlyList<PlannedRun>? Plan(int rating, int target, IReadOnlyDictionary<int, double> bests, int levelCap)
    {
        var need = target - rating;
        if (need <= 0)
        {
            return [];
        }
        for (var cap = Math.Max(KeystoneRating.MinLevel, levelCap); cap <= KeystoneRating.MaxLevel; cap++)
        {
            // The fewest dungeons that get there at this level ...
            var picked = new List<(int MapId, int Level)>();
            var sum = 0.0;
            foreach (var (mapId, best) in bests.OrderByDescending(b => KeystoneRating.Base(cap) - b.Value).ThenBy(b => b.Key))
            {
                var gain = KeystoneRating.Base(cap) - best;
                if (gain <= 0)
                {
                    break;
                }
                picked.Add((mapId, cap));
                sum += gain;
                if (sum >= need)
                {
                    return Lower(picked, bests, need);
                }
            }
        }
        return null;
    }

    /// <summary>... then as low as each can go: one level off the highest run while the sum still holds.</summary>
    private static List<PlannedRun> Lower(List<(int MapId, int Level)> runs, IReadOnlyDictionary<int, double> bests, int need)
    {
        double Sum() => runs.Sum(r => Math.Max(0, KeystoneRating.Base(r.Level) - bests[r.MapId]));
        var lowered = true;
        while (lowered)
        {
            lowered = false;
            foreach (var i in Enumerable.Range(0, runs.Count).OrderByDescending(i => runs[i].Level))
            {
                if (runs[i].Level <= KeystoneRating.MinLevel)
                {
                    continue;
                }
                runs[i] = (runs[i].MapId, runs[i].Level - 1);
                if (Sum() >= need)
                {
                    lowered = true;
                    break;
                }
                runs[i] = (runs[i].MapId, runs[i].Level + 1);
            }
        }
        return runs
            .Select(r => new PlannedRun(r.MapId, r.Level, (int)Math.Round(KeystoneRating.Base(r.Level) - bests[r.MapId])))
            .OrderByDescending(r => r.Level).ThenByDescending(r => r.Gain)
            .ToList();
    }
}
