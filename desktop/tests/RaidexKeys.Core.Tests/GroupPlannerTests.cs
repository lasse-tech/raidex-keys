// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using RaidexKeys.Core.Rating;

namespace RaidexKeys.Core.Tests;

/// <summary>The group planner's rules (plan 7.2b) on a small made-up guild.</summary>
public sealed class GroupPlannerTests
{
    private const int Dungeon = 542;

    private static readonly GroupOptions Plain = new(BySpec: false, ByItemLevel: false, Allowance: 0, _ => null);

    /// <param name="best">Their best score in the one dungeon; the lower, the more a run pays them.</param>
    private static GroupCandidate Who(string name, double best, GroupRole role = GroupRole.Damage,
        int? keyLevel = null, int? itemLevel = null) =>
        new(name, name, "testrealm", null, role, 0, itemLevel, keyLevel is null ? null : Dungeon, keyLevel,
            new Dictionary<int, double> { [Dungeon] = best });

    [Fact]
    public void TheWorstOffGainsMost()
    {
        // Base(+10) is 275: the four with 100 gain 175, the one with 270 gains 5.
        var candidates = new List<GroupCandidate>
        {
            Who("Holder", 100, keyLevel: 10),
            Who("Alpha", 100), Who("Beta", 100), Who("Gamma", 100),
            Who("Barely", 270), Who("Delta", 120),
        };

        var group = Assert.Single(GroupPlanner.Plan(candidates, Plain));

        // Barely is left at home although the group would still be five without them.
        Assert.DoesNotContain("Barely", group.Seats.Select(s => s.Who.Name));
        Assert.True(group.Worst > 0);
        Assert.Equal("Holder", group.Holder.Name);
    }

    [Fact]
    public void SpecTakesOneTankOneHealerAndThreeDamageDealers()
    {
        var candidates = new List<GroupCandidate>
        {
            Who("Holder", 100, GroupRole.Tank, keyLevel: 10),
            Who("Healer", 100, GroupRole.Healer),
            Who("Damage1", 100), Who("Damage2", 100), Who("Damage3", 100),
            // Gains most of all, but a second tank has no seat.
            Who("Tank2", 0, GroupRole.Tank),
        };

        var group = Assert.Single(GroupPlanner.Plan(candidates, Plain with { BySpec = true }));
        Assert.DoesNotContain("Tank2", group.Seats.Select(s => s.Who.Name));

        // Without a healer among them, no group comes out at all.
        var healerless = candidates.Where(c => c.Role != GroupRole.Healer).ToList();
        Assert.Empty(GroupPlanner.Plan(healerless, Plain with { BySpec = true }));
    }

    [Fact]
    public void FewerThanFiveIsNoGroup()
    {
        var four = new List<GroupCandidate>
        {
            Who("Holder", 100, keyLevel: 10),
            Who("Alpha", 100), Who("Beta", 100), Who("Gamma", 100),
        };

        Assert.Empty(GroupPlanner.Plan(four, Plain));
        Assert.Single(GroupPlanner.Plan([.. four, Who("Delta", 100)], Plain));
    }

    [Fact]
    public void ItemLevelMarksTheTooLightAndKeepsThem()
    {
        var candidates = new List<GroupCandidate>
        {
            Who("Holder", 100, keyLevel: 10, itemLevel: 700),
            Who("Alpha", 100, itemLevel: 700), Who("Beta", 100, itemLevel: 700),
            Who("Gamma", 100, itemLevel: 700),
            // Gains the most of all, so the group cannot do without them.
            Who("Light", 0, itemLevel: 680),
            // Nobody knows theirs, so nobody can call them too light.
            Who("Unknown", 100),
        };
        // A +10 rewards item level 700, and up to 5 below still counts.
        var options = Plain with { ByItemLevel = true, Allowance = 5, Recommended = _ => 700 };

        var group = Assert.Single(GroupPlanner.Plan(candidates, options));

        // Five of the six come, and whoever is light is said so, not dropped.
        Assert.Equal(GroupPlanner.GroupSize, group.Seats.Count);
        Assert.True(Assert.Single(group.Seats, s => s.Who.Name == "Light").TooLight);
        Assert.All(group.Seats.Where(s => s.Who.Name != "Light"), s => Assert.False(s.TooLight));
    }
}
