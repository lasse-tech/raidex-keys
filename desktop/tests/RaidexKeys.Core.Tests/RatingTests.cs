// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.Rating;

namespace RaidexKeys.Core.Tests;

/// <summary>Public runs from Raider.IO (2026-09-11) and what the game rated them.</summary>
public sealed class RatingTests
{
    [Theory]
    [InlineData(21, 1849488, 1920999, 501.4)]   // timed, near the top
    [InlineData(4, 1203693, 1800999, 197.4)]    // below the first affix level
    [InlineData(9, 2007336, 1800999, 270.7)]    // untimed
    [InlineData(14, 2004030, 1680999, 312.8)]   // untimed above +11 counts as +11
    public void ScoresMatchTheGame(int level, int clearMs, int parMs, double score) =>
        // The addon stores whole seconds, which moves a score by a few hundredths at most.
        Assert.InRange(KeystoneRating.Score(level, clearMs / 1000, parMs / 1000), score - 0.15, score + 0.15);

    [Fact]
    public void PlanTakesTheFewestRunsAtTheLowestLevels()
    {
        // 240 + 290 + 320: +10 in the weakest dungeon alone gives 80, so 100 needs two runs.
        var bests = new Dictionary<int, double> { [1] = 240, [2] = 290, [3] = 320 };
        var plan = RatingPlanner.Plan(rating: 850, target: 950, bests, levelCap: 10)!;

        Assert.Equal([1, 2], plan.Select(r => r.MapId).Order());
        Assert.True(plan.Sum(r => r.Gain) >= 100);
        Assert.All(plan, r => Assert.True(r.Level <= 10));
        Assert.Equal(2800, RatingPlanner.DefaultTarget(2648));
    }
    [Fact]
    public void EveryPlannedRunAddsSomething()
    {
        // #50: a run is lowered only while the sum still holds, and the sum
        // counts a run without gain as 0 - so the others would have to hold it
        // alone, and then it would never have been picked. Pinned on many plans.
        var random = new Random(50);
        for (var n = 0; n < 5000; n++)
        {
            var bests = Enumerable.Range(1, 8).ToDictionary(i => i,
                _ => random.Next(3) == 0 ? 0.0 : Math.Round(150 + (random.NextDouble() * 250), 1));
            var rating = (int)bests.Values.Sum();
            var plan = RatingPlanner.Plan(rating, rating + random.Next(1, 400), bests, random.Next(2, 16));
            Assert.All(plan ?? [], r => Assert.True(r.Gain > 0, $"{r} adds nothing"));
        }
    }
}
