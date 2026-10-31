// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.SavedVariables;

/// <summary>A sign-up to a raid of the game's calendar, made with the addon.</summary>
/// <param name="Role">'T', 'H' or 'D'.</param>
/// <param name="Assigned">The role was set by the raid's creator, a moderator or the guild master.</param>
public sealed record GameRaidSignup(string Who, char Role, WowClass? Class, int? ItemLevel, bool Assigned, string? Note);

/// <summary>A guild raid from the game's calendar (addon Core/Raids.lua).</summary>
/// <param name="Guild">"Name-Realm".</param>
/// <param name="Key">"&lt;event id&gt;-&lt;yyyymmdd&gt;".</param>
/// <param name="DifficultyId">The game's difficulty id: 17 LFR, 14 normal, 15 heroic, 16 mythic.</param>
public sealed record GameRaid(string Guild, string Key, string Title, DateTimeOffset At, string? Creator,
    string? Instance, string? Difficulty, int? DifficultyId, int? Killed, int? Bosses,
    IReadOnlyList<GameRaidSignup> Signups);

public static class GameRaids
{
    /// <summary>RaidexKeysDB.raids: every guild's calendar with the addon's sign-ups and progress.</summary>
    public static List<GameRaid> Read(LuaTable? raids)
    {
        var list = new List<GameRaid>();
        if (raids is null)
        {
            return list;
        }
        foreach (var (guildKey, value) in raids.Entries)
        {
            if (guildKey is not string guild || value is not LuaTable g || g.Table("calendar") is not { } calendar)
            {
                continue;
            }
            var events = g.Table("events");
            var progress = g.Table("progress");
            foreach (var (k, v) in calendar.Entries)
            {
                if (k is not string key || v is not LuaTable raid || raid.Integer("at") is not { } at || at <= 0)
                {
                    continue;
                }
                var ev = events?.Table(key);
                var map = raid.Integer("instanceMap") ?? ev?.Integer("instanceMap");
                var difficulty = raid.Integer("difficultyId") ?? ev?.Integer("difficultyId");
                var done = map is { } m && difficulty is { } d ? progress?.Table($"{m}|{d}") : null;
                list.Add(new GameRaid(guild, key, raid.String("title") ?? "",
                    DateTimeOffset.FromUnixTimeSeconds(at), raid.String("creator"),
                    raid.String("instanceName") ?? ev?.String("instanceName"),
                    raid.String("difficultyName") ?? ev?.String("difficultyName"),
                    (int?)difficulty, (int?)done?.Integer("killed"), (int?)done?.Integer("bosses"),
                    Signups(ev?.Table("signups"))));
            }
        }
        return list;
    }

    private static List<GameRaidSignup> Signups(LuaTable? signups) =>
        (signups?.Entries.Values ?? []).OfType<LuaTable>()
            .Where(s => s.Boolean("removed") != true && s.String("who") is not null)
            .Select(s => new GameRaidSignup(s.String("who")!, s.String("role") is { Length: > 0 } r ? r[0] : 'D',
                ClassOf(s.String("classFile")), (int?)s.Integer("itemLevel"), s.Boolean("assigned") == true,
                s.String("note") is { Length: > 0 } note ? note : null))
            .OrderBy(s => s.Who, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>The game's class file name ("DEATHKNIGHT") as a class.</summary>
    public static WowClass? ClassOf(string? classFile) => classFile switch
    {
        "WARRIOR" => WowClass.Warrior,
        "PALADIN" => WowClass.Paladin,
        "HUNTER" => WowClass.Hunter,
        "ROGUE" => WowClass.Rogue,
        "PRIEST" => WowClass.Priest,
        "DEATHKNIGHT" => WowClass.DeathKnight,
        "SHAMAN" => WowClass.Shaman,
        "MAGE" => WowClass.Mage,
        "WARLOCK" => WowClass.Warlock,
        "MONK" => WowClass.Monk,
        "DRUID" => WowClass.Druid,
        "DEMONHUNTER" => WowClass.DemonHunter,
        "EVOKER" => WowClass.Evoker,
        _ => null,
    };

    /// <summary>Several accounts' files as one: per raid, the one with the most sign-ups.</summary>
    public static List<GameRaid> Merge(IEnumerable<IReadOnlyList<GameRaid>> parts) =>
        parts.SelectMany(p => p)
            .GroupBy(r => (r.Guild, r.Key))
            .Select(g => g.MaxBy(r => r.Signups.Count)!)
            .OrderBy(r => r.At)
            .ToList();
}
