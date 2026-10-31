// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.Mdt;

// Mythic Dungeon Tools (MDT, GPL v2) is never part of this app: its files are
// read at runtime from the player's own installation, as data, and nothing of
// it is run, copied or shipped.

/// <summary>One spawn of an enemy: its forces and where it stands on MDT's map.</summary>
/// <param name="Count">Forces - a clone may be worth other than its enemy.</param>
/// <param name="X">From the map's left edge, 0 to 840.</param>
/// <param name="Y">From the map's top edge, 0 to -555: MDT counts downwards negative.</param>
/// <param name="Sublevel">The floor, 1 for the first.</param>
public sealed record MdtClone(int Count, double X, double Y, int Sublevel, double Scale = 1);

/// <summary>An enemy of a dungeon, as MDT's data lists it.</summary>
/// <param name="Scale">How large MDT draws it, 1 for most.</param>
public sealed record MdtEnemy(string Name, int NpcId, IReadOnlyDictionary<int, MdtClone> Clones, double Scale = 1, bool IsBoss = false);

/// <summary>A dungeon in MDT's data.</summary>
/// <param name="Index">MDT's own dungeon number, which routes are filed under.</param>
/// <param name="MapId">The challenge-mode id, as the game and RaidexKeys.lua use it.</param>
/// <param name="TotalCount">The forces that make 100 %.</param>
/// <param name="TileDirs">Floor to the folder of its 150 map tiles in the installed MDT; empty when unknown.</param>
public sealed record MdtDungeon(int Index, int MapId, string EnglishName, int TotalCount, IReadOnlyDictionary<int, MdtEnemy> Enemies)
{
    public IReadOnlyDictionary<int, string> TileDirs { get; init; } = new Dictionary<int, string>();

    /// <summary>MDT's size factor for this dungeon's enemies.</summary>
    public double ScaleMultiplier { get; init; } = 1;
}

/// <summary>One pull: enemy index to the clones of it pulled.</summary>
public sealed record MdtPull(IReadOnlyDictionary<int, IReadOnlyList<int>> Clones);

/// <summary>A route (MDT: "preset") of the player.</summary>
/// <param name="Name">Null for MDT's first route when never renamed - "Default" in MDT's language.</param>
/// <param name="Selected">The route chosen in MDT for its dungeon.</param>
public sealed record MdtRoute(int DungeonIndex, string? Name, string? Uid, bool Selected, IReadOnlyList<MdtPull> Pulls)
{
    /// <summary>
    /// The preset as it stands, every field of it - what an export string is
    /// made of (<see cref="MdtString.Export"/>). The route above is the part
    /// this app reads; this is the whole of what MDT wrote.
    /// </summary>
    public SavedVariables.LuaTable? Preset { get; init; }
}

/// <summary>A pull's forces and the enemies in it, as "3 × Name".</summary>
public sealed record PullForces(int Number, int Count, int Cumulative, IReadOnlyList<(string Name, int Kills)> Enemies);

/// <summary>What a route is worth in its dungeon.</summary>
public sealed record RouteForces(IReadOnlyList<PullForces> Pulls, int Count, int DungeonTotal)
{
    /// <summary>Enemy forces in percent: 101.4 for a route a little over the requirement.</summary>
    public double Percent => DungeonTotal > 0 ? 100.0 * Count / DungeonTotal : 0;

    /// <summary>
    /// Adds up each pull's clones - the forces of the clone, else of its enemy.
    /// Enemies and clones the dungeon no longer has count nothing, as in MDT.
    /// </summary>
    public static RouteForces Of(MdtRoute route, MdtDungeon dungeon)
    {
        var pulls = new List<PullForces>();
        var total = 0;
        foreach (var pull in route.Pulls)
        {
            var count = 0;
            var kills = new List<(string Name, int Kills)>();
            foreach (var (enemyIndex, clones) in pull.Clones.OrderBy(c => c.Key))
            {
                if (!dungeon.Enemies.TryGetValue(enemyIndex, out var enemy))
                {
                    continue;
                }
                var known = clones.Where(enemy.Clones.ContainsKey).ToList();
                if (known.Count == 0)
                {
                    continue;
                }
                count += known.Sum(c => enemy.Clones[c].Count);
                kills.Add((enemy.Name, known.Count));
            }
            total += count;
            // The same enemy can stand under several indices; one line per name.
            var byName = kills.GroupBy(k => k.Name).Select(g => (g.Key, g.Sum(k => k.Kills))).ToList();
            pulls.Add(new PullForces(pulls.Count + 1, count, total, byName));
        }
        return new RouteForces(pulls, total, dungeon.TotalCount);
    }
}

/// <summary>Everything read from MDT: its dungeons and the player's routes.</summary>
public sealed record MdtData(bool Installed, IReadOnlyDictionary<int, MdtDungeon> DungeonsByIndex, IReadOnlyList<MdtRoute> Routes)
{
    public static readonly MdtData None = new(false, new Dictionary<int, MdtDungeon>(), []);

    /// <summary>The installed MDT's version as an export carries it (6.2.16 → 6216); null when unknown.</summary>
    public int? Version { get; init; }

    public MdtDungeon? DungeonOf(MdtRoute route) => DungeonsByIndex.GetValueOrDefault(route.DungeonIndex);
}
