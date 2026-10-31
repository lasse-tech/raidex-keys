// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.SavedVariables;

/// <summary>Turns RaidexKeys.lua into <see cref="AddonData"/>.</summary>
public static class AddonDataReader
{
    public const string FileName = "RaidexKeys.lua";

    /// <summary>Reads one file. Throws on a file that is not (yet) complete.</summary>
    public static AddonData Read(string path)
    {
        var globals = LuaParser.ParseGlobals(File.ReadAllText(path));
        if (!globals.TryGetValue("RaidexKeysDB", out var value) || value is not LuaTable db)
        {
            throw new FormatException($"{path}: no RaidexKeysDB");
        }
        var schema = db.Integer("schema") ?? 0;
        if (schema > AddonData.SupportedSchema)
        {
            throw new NotSupportedException($"{path}: schema {schema} is newer than this app ({AddonData.SupportedSchema})");
        }

        return new AddonData
        {
            Region = db.String("region"),
            Season = (int?)db.Integer("season"),
            ResetAt = Time(db.Integer("resetAt")),
            Affixes = db.Table("affixes")?.List().OfType<long>().Select(a => (int)a).ToList() ?? [],
            Maps = ReadMaps(db.Table("maps")),
            Rewards = ReadRewards(db.Table("rewards")),
            Characters = ReadCharacters(db.Table("chars"), path),
            Guilds = ReadGuilds(db.Table("guilds")),
            Raids = GameRaids.Read(db.Table("raids")),
            InboxAnswers = (db.Table("inbox")?.Entries ?? new Dictionary<object, object?>())
                .Where(e => e.Key is string && e.Value is LuaTable t && t.String("state") is not null)
                .ToDictionary(e => (string)e.Key, e => ((LuaTable)e.Value!).String("state")!),
            Sources = [path],
        };
    }

    private static DateTimeOffset? Time(long? serverTime) =>
        serverTime is { } s and > 0 ? DateTimeOffset.FromUnixTimeSeconds(s) : null;

    private static IEnumerable<(long Id, LuaTable Table)> Tables(LuaTable? table) =>
        table is null
            ? []
            : table.Entries.Where(e => e.Key is long && e.Value is LuaTable)
                .Select(e => ((long)e.Key, (LuaTable)e.Value!));

    private static Dictionary<int, DungeonInfo> ReadMaps(LuaTable? maps) =>
        Tables(maps)
            .Where(m => m.Table.String("name") is not null)
            .ToDictionary(m => (int)m.Id,
                m => new DungeonInfo((int)m.Id, m.Table.String("name")!, (int)(m.Table.Integer("timeLimit") ?? 0)));

    /// <summary>What each key level rewards; the addon writes it from 0.1.10 on.</summary>
    private static Dictionary<int, RewardLevel> ReadRewards(LuaTable? rewards) =>
        Tables(rewards)
            .Where(r => r.Table.Integer("itemLevel") is > 0)
            .ToDictionary(r => (int)r.Id,
                r => new RewardLevel((int)r.Table.Integer("itemLevel")!.Value, (int?)r.Table.Integer("vaultItemLevel")));

    private static List<AddonCharacter> ReadCharacters(LuaTable? chars, string path)
    {
        var list = new List<AddonCharacter>();
        if (chars is null)
        {
            return list;
        }
        foreach (var (key, value) in chars.Entries)
        {
            if (key is not string guid || value is not LuaTable c || c.String("name") is not { } name)
            {
                continue;
            }
            var keystone = c.Table("key");
            var classId = c.Integer("classId");
            list.Add(new AddonCharacter(
                guid, name, c.String("realm") ?? "",
                classId is >= 1 and <= 13 ? (WowClass)classId : null,
                (int)(c.Integer("level") ?? 0),
                (int?)keystone?.Integer("mapId"), (int?)keystone?.Integer("level"),
                (int)Math.Round(c.Number("rating") ?? 0),
                Tables(c.Table("best")).ToDictionary(b => (int)b.Id, b => new BestRun(
                    (int)(b.Table.Integer("level") ?? 0),
                    (int)(b.Table.Integer("durationSec") ?? 0),
                    b.Table.Boolean("overTime") ?? false,
                    b.Table.Number("score") ?? 0)),
                c.Table("vault")?.List().OfType<LuaTable>().Select(v => new VaultSlot(
                    (int)(v.Integer("threshold") ?? 0),
                    (int)(v.Integer("progress") ?? 0),
                    (int)(v.Integer("level") ?? 0),
                    (int?)v.Integer("itemLevel"))).ToList() ?? [],
                Time(c.Integer("updatedAt")) ?? DateTimeOffset.MinValue,
                c.Table("week")?.List().OfType<LuaTable>()
                    .Select(r => new WeekRun((int)(r.Integer("mapId") ?? 0), (int)(r.Integer("level") ?? 0), r.Boolean("timed") ?? false))
                    .Where(r => r.Level > 0)
                    .OrderByDescending(r => r.Level)
                    .ToList())
            {
                Id = c.String("id"),
                Guild = c.String("guild"),
                Source = path,
            });
        }
        return list;
    }

    private static List<AddonGuild> ReadGuilds(LuaTable? guilds)
    {
        var list = new List<AddonGuild>();
        if (guilds is null)
        {
            return list;
        }
        foreach (var value in guilds.Entries.Values)
        {
            if (value is not LuaTable g || g.String("name") is not { } name)
            {
                continue;
            }
            var keys = new List<GuildKey>();
            foreach (var (who, entry) in g.Table("keys")?.Entries ?? new Dictionary<object, object?>())
            {
                if (who is string character && entry is LuaTable k)
                {
                    keys.Add(new GuildKey(character, (int?)k.Integer("mapId"), (int?)k.Integer("level"),
                        (int)(k.Integer("rating") ?? 0), Time(k.Integer("seenAt")) ?? DateTimeOffset.MinValue));
                }
            }
            list.Add(new AddonGuild(name, g.String("realm") ?? "", keys));
        }
        return list;
    }
}
