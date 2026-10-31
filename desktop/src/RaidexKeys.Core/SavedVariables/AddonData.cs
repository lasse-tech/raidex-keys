// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

namespace RaidexKeys.Core.SavedVariables;

/// <summary>A season dungeon: challenge-mode id, name in the client's language, timer.</summary>
public sealed record DungeonInfo(int MapId, string Name, int TimeLimitSec);

/// <summary>The best run of one dungeon this season.</summary>
public sealed record BestRun(int Level, int DurationSec, bool OverTime, double Score);

/// <summary>One slot of the Great Vault's dungeon row.</summary>
/// <param name="Threshold">Runs it takes: 1, 4 or 8.</param>
/// <param name="Level">The key level its reward is for, once unlocked: the lowest of the week's best runs it counts.</param>
/// <param name="ItemLevel">The reward's item level; the addon stores it from 0.1.6 on.</param>
public sealed record VaultSlot(int Threshold, int Progress, int Level, int? ItemLevel = null)
{
    public bool Unlocked => Progress >= Threshold;
    public int RunsMissing => Math.Max(0, Threshold - Progress);
}

/// <summary>
/// What a keystone level pays out this season, as the addon reads it from the
/// game (0.1.10 on): the item level of the run's own reward, and of its slot
/// in the Great Vault.
/// </summary>
public sealed record RewardLevel(int ItemLevel, int? VaultItemLevel);

/// <summary>A run of the current week, as the addon stores it from 0.1.6 on.</summary>
public sealed record WeekRun(int MapId, int Level, bool Timed);

/// <summary>A character of the account, as the addon last saw it.</summary>
public sealed record AddonCharacter(
    string Guid, string Name, string Realm, WowClass? Class, int Level,
    int? KeyMapId, int? KeyLevel, int Rating,
    IReadOnlyDictionary<int, BestRun> Best, IReadOnlyList<VaultSlot> Vault,
    DateTimeOffset UpdatedAt, IReadOnlyList<WeekRun>? Week = null)
{
    /// <summary>"Name-Realm" as the guild's sign-ups name the character; empty from an addon before the sign-ups from the app.</summary>
    public string? Id { get; init; }

    /// <summary>"Guild-Realm" as RaidexKeysDB.raids keys it; empty from an addon before the sign-ups from the app.</summary>
    public string? Guild { get; init; }

    /// <summary>The file this character was read from: its WoW account's.</summary>
    public string? Source { get; init; }

    /// <summary>The week's runs, highest first; empty from an addon before 0.1.6.</summary>
    public IReadOnlyList<WeekRun> WeekRuns => Week ?? [];

    /// <summary>
    /// The vault as it stands in the week starting <paramref name="weekStart"/>:
    /// a snapshot from an earlier week says nothing about this one, whose
    /// slots all start empty.
    /// </summary>
    public IReadOnlyList<VaultSlot> VaultIn(DateTimeOffset? weekStart) =>
        weekStart is { } start && UpdatedAt < start
            ? Vault.Select(s => s with { Progress = 0, Level = 0, ItemLevel = null }).ToList()
            : Vault;

    /// <summary>
    /// Slots unlocked in a week before <paramref name="weekStart"/>, seen last
    /// then: nobody has logged in since, so their reward still waits in the vault.
    /// </summary>
    public int RewardsWaiting(DateTimeOffset? weekStart) =>
        weekStart is { } start && UpdatedAt < start ? Vault.Count(s => s.Unlocked && s.Level > 0) : 0;

    /// <summary>
    /// A rating above 0 is only earned by running keys, so a character with
    /// one has runs this season. Stored without a single run, the data is
    /// incomplete - the game had not handed them out when the addon asked -
    /// and never means "no runs yet".
    /// </summary>
    public bool IsIncomplete => Rating > 0 && Best.Count == 0;
}

/// <summary>A guild member's keystone, heard through the LibKeystone protocol.</summary>
public sealed record GuildKey(string Character, int? MapId, int? Level, int Rating, DateTimeOffset SeenAt)
{
    /// <summary>"Name" of "Name-Realm".</summary>
    public string Name => Character.Split('-', 2)[0];
}

public sealed record AddonGuild(string Name, string Realm, IReadOnlyList<GuildKey> Keys);

/// <summary>
/// Everything the addon wrote into RaidexKeysDB (schema 1), across all WoW
/// accounts found - see addon/RaidexKeys/Core/Database.lua for the layout.
/// </summary>
public sealed class AddonData
{
    public const int SupportedSchema = 1;

    public string? Region { get; init; }
    public int? Season { get; init; }
    /// <summary>The next weekly reset as the addon last knew it; may lie in the past.</summary>
    public DateTimeOffset? ResetAt { get; init; }
    public IReadOnlyList<int> Affixes { get; init; } = [];
    public IReadOnlyDictionary<int, DungeonInfo> Maps { get; init; } = new Dictionary<int, DungeonInfo>();
    public IReadOnlyList<AddonCharacter> Characters { get; init; } = [];
    public IReadOnlyList<AddonGuild> Guilds { get; init; } = [];

    /// <summary>The guild raids of the game's calendar, by start; empty before addon 0.5.0.</summary>
    public IReadOnlyList<GameRaid> Raids { get; init; } = [];

    /// <summary>What became of the app's sign-up requests, by id: "applied", "failed", "dismissed" or "gone".</summary>
    public IReadOnlyDictionary<string, string> InboxAnswers { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// What each keystone level rewards this season, by level; empty from an
    /// addon before 0.1.10.
    /// </summary>
    public IReadOnlyDictionary<int, RewardLevel> Rewards { get; init; } = new Dictionary<int, RewardLevel>();

    /// <summary>The files this was read from.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    public static AddonData Empty { get; } = new();

    /// <summary>
    /// The item level a key of this level suggests: what the game hands out
    /// for it. Blizzard recommends none, and this is the closest the game
    /// says (plan 7.2b) - in Midnight the Great Vault's level, which sits a
    /// few points above what the run itself drops, so the group planner's
    /// allowance is set to cover the gap. Null while the addon has not read
    /// the table; a level above it takes the highest one known.
    /// </summary>
    public int? RecommendedItemLevel(int keyLevel)
    {
        if (Rewards.Count == 0)
        {
            return null;
        }
        if (Rewards.TryGetValue(keyLevel, out var exact))
        {
            return exact.ItemLevel;
        }
        var below = Rewards.Where(r => r.Key <= keyLevel).OrderByDescending(r => r.Key).Select(r => r.Value).FirstOrDefault();
        return below?.ItemLevel ?? Rewards.OrderBy(r => r.Key).First().Value.ItemLevel;
    }

    /// <summary>The start of the keystone week that contains <paramref name="now"/>.</summary>
    /// <remarks>
    /// Resets come every seven days, so the stored reset - even one long past,
    /// when nobody logged in since - pins the week of any moment.
    /// </remarks>
    public DateTimeOffset? WeekStart(DateTimeOffset now)
    {
        if (ResetAt is not { } reset)
        {
            return null;
        }
        var week = TimeSpan.FromDays(7);
        var next = reset;
        while (next <= now)
        {
            next += week;
        }
        while (next - week > now)
        {
            next -= week;
        }
        return next - week;
    }

    /// <summary>Several files (one per WoW account) as one: the newest wins.</summary>
    public static AddonData Merge(IReadOnlyList<AddonData> parts)
    {
        if (parts.Count == 0)
        {
            return Empty;
        }
        if (parts.Count == 1)
        {
            return parts[0];
        }
        var newest = parts.OrderByDescending(p => p.Characters.Select(c => c.UpdatedAt).DefaultIfEmpty().Max()).First();
        return new AddonData
        {
            Region = newest.Region,
            Season = newest.Season,
            ResetAt = parts.Select(p => p.ResetAt).Max(),
            Affixes = newest.Affixes,
            Maps = newest.Maps,
            // As with the maps: the newest file's, and another account's only
            // while the newest has none - they change with the season.
            Rewards = newest.Rewards.Count > 0
                ? newest.Rewards
                : parts.Select(p => p.Rewards).FirstOrDefault(r => r.Count > 0) ?? newest.Rewards,
            Characters = parts.SelectMany(p => p.Characters)
                .GroupBy(c => c.Guid).Select(g => g.MaxBy(c => c.UpdatedAt)!).ToList(),
            Guilds = parts.SelectMany(p => p.Guilds)
                .GroupBy(g => (g.Name, g.Realm))
                .Select(g => new AddonGuild(g.Key.Name, g.Key.Realm, g.SelectMany(x => x.Keys)
                    .GroupBy(k => k.Character).Select(k => k.MaxBy(x => x.SeenAt)!).ToList()))
                .ToList(),
            Raids = GameRaids.Merge(parts.Select(p => p.Raids)),
            InboxAnswers = parts.SelectMany(p => p.InboxAnswers).DistinctBy(a => a.Key).ToDictionary(),
            Sources = parts.SelectMany(p => p.Sources).ToList(),
        };
    }
}
