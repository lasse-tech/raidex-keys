// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.Rating;

/// <summary>What a specialisation does in a Mythic+ group.</summary>
public enum GroupRole
{
    /// <summary>Nobody said - the character is not in Raidex's roster, or was never scanned.</summary>
    Unknown,
    Tank,
    Healer,
    Damage,
}

/// <summary>Somebody who could come along tonight, with what their evening is worth.</summary>
/// <param name="Id">Tells two candidates apart: "name:realm" as the guild page keys them.</param>
/// <param name="Rating">Their Mythic+ rating this season - shown beside them, never weighed.</param>
/// <param name="ItemLevel">Their equipped item level; null when Raidex has none.</param>
/// <param name="Best">The best score per dungeon this season; a dungeon missing counts as never run.</param>
public sealed record GroupCandidate(
    string Id, string Name, string Realm, WowClass? Class, GroupRole Role, int Rating, int? ItemLevel,
    int? KeyMapId, int? KeyLevel, IReadOnlyDictionary<int, double> Best)
{
    public bool HasKey => KeyMapId is > 0 && KeyLevel is > 0;

    public double BestIn(int mapId) => Best.TryGetValue(mapId, out var score) ? score : 0;

    /// <summary>What timing this key right on the timer would add to their rating.</summary>
    public int GainFrom(int mapId, int level) =>
        (int)Math.Round(Math.Max(0, KeystoneRating.Base(level) - BestIn(mapId)));
}

/// <summary>One of the five, and what the run pays them.</summary>
/// <param name="TooLight">
/// Their gear is under what the key rewards, by more than the allowance -
/// said beside them, never a reason to leave them at home (Lars, 2026-09-12).
/// </param>
public sealed record GroupSeat(GroupCandidate Who, int Gain, bool HoldsKey, bool TooLight = false);

/// <summary>The group for one keystone: who comes, and what each of them gains.</summary>
public sealed record GroupProposal(int MapId, int Level, IReadOnlyList<GroupSeat> Seats)
{
    public GroupCandidate Holder => Seats.First(s => s.HoldsKey).Who;

    /// <summary>What the worst-off of the five gains - what the group is judged by.</summary>
    public int Worst => Seats.Min(s => s.Gain);

    public int Total => Seats.Sum(s => s.Gain);
}

/// <param name="BySpec">Exactly one tank, one healer and three damage dealers, by their current spec.</param>
/// <param name="ByItemLevel">Point out a member whose gear is light for the key. Nobody is left out for it.</param>
/// <param name="Allowance">How far below the recommended item level still counts as fine.</param>
/// <param name="Recommended">
/// The item level a key level suggests - the game's own reward for it
/// (<see cref="SavedVariables.AddonData.RecommendedItemLevel"/>); null where
/// the addon has not read it, and then that key takes everyone.
/// </param>
public sealed record GroupOptions(bool BySpec, bool ByItemLevel, int Allowance, Func<int, int?> Recommended);

/// <summary>
/// The group in which everyone gains as much rating as possible (plan 7.2b):
/// out of the candidates the user picked, five around one of their keystones.
/// The best group is the one whose worst-off member gains most; at a tie the
/// larger total wins - so no group where four gain and one gets nothing.
/// The item level never decides who comes: a member whose gear is light for
/// the key is marked, and the evening is theirs to call.
/// </summary>
/// <remarks>
/// What a key pays a member depends on that member alone - their own best run
/// in that dungeon - and not on who else comes. So the best five for a key are
/// its holder plus the highest gains left, per role where the group is built by
/// spec: picking each seat on its own is exactly the best of every combination,
/// without walking through them.
/// </remarks>
public static class GroupPlanner
{
    public const int GroupSize = 5;

    private const int Tanks = 1, Healers = 1, DamageDealers = 3;

    /// <summary>One group per keystone worth running, the best first.</summary>
    public static IReadOnlyList<GroupProposal> Plan(IReadOnlyList<GroupCandidate> candidates, GroupOptions options)
    {
        var proposals = new List<GroupProposal>();
        foreach (var holder in candidates.Where(c => c.HasKey))
        {
            var mapId = holder.KeyMapId!.Value;
            var level = holder.KeyLevel!.Value;
            var others = candidates
                .Where(c => c.Id != holder.Id)
                .OrderByDescending(c => c.GainFrom(mapId, level))
                .ThenBy(c => c.Name, StringComparer.CurrentCulture)
                .ToList();
            var group = options.BySpec ? BySpec(holder, others) : Best(others, GroupSize - 1);
            if (group is null)
            {
                continue;
            }
            var floor = Floor(options, level);
            var seats = group.Prepend(holder)
                .Select(c => new GroupSeat(c, c.GainFrom(mapId, level), c.Id == holder.Id, !Geared(c, floor)))
                .ToList();
            proposals.Add(new GroupProposal(mapId, level, seats));
        }
        return proposals
            .OrderByDescending(p => p.Worst).ThenByDescending(p => p.Total).ThenByDescending(p => p.Level)
            .ToList();
    }

    /// <summary>
    /// The <paramref name="seats"/> highest gains, or null when there are not
    /// that many left: a group of four is not a Mythic+ group, and offering
    /// one hides that the item level or the roles left nobody over.
    /// </summary>
    private static List<GroupCandidate>? Best(List<GroupCandidate> others, int seats) =>
        others.Count >= seats ? others.Take(seats).ToList() : null;

    /// <summary>The item level a key of this level wants, or null while nobody is asking.</summary>
    private static int? Floor(GroupOptions options, int level) =>
        options.ByItemLevel && options.Recommended(level) is { } recommended
            ? recommended - Math.Max(0, options.Allowance)
            : null;

    /// <summary>Gear enough for the key - and anyone whose item level nobody knows.</summary>
    /// <remarks>Only ever said beside a name; the group is built without it.</remarks>
    private static bool Geared(GroupCandidate candidate, int? floor) =>
        floor is not { } bar || candidate.ItemLevel is not { } level || level >= bar;

    /// <summary>
    /// The four beside the holder as the roles want them; null when the picked
    /// candidates do not hold a tank, a healer and three damage dealers -
    /// somebody whose spec Raidex does not know among them.
    /// </summary>
    /// <param name="others">Everyone but the holder, by gain, highest first.</param>
    private static List<GroupCandidate>? BySpec(GroupCandidate holder, List<GroupCandidate> others)
    {
        var open = new Dictionary<GroupRole, int>
        {
            [GroupRole.Tank] = Tanks,
            [GroupRole.Healer] = Healers,
            [GroupRole.Damage] = DamageDealers,
        };
        if (holder.Role == GroupRole.Unknown || --open[holder.Role] < 0)
        {
            return null;
        }
        var group = new List<GroupCandidate>();
        foreach (var (role, seats) in open.Where(r => r.Value > 0))
        {
            if (Best(others.Where(c => c.Role == role).ToList(), seats) is not { } found)
            {
                return null;
            }
            group.AddRange(found);
        }
        return group;
    }
}
