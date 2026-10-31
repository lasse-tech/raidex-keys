// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using RaidexKeys.App.Localization;
using RaidexKeys.App.ViewModels;
using RaidexKeys.Core;
using RaidexKeys.Core.Mdt;
using RaidexKeys.Core.Raidex;
using RaidexKeys.Core.Rating;
using RaidexKeys.Core.SavedVariables;
using RaidexKeys.Core.Web;

namespace RaidexKeys.App;

/// <summary>Turns what the addon saved into the pages' view models.</summary>
internal static class LiveData
{
    /// <summary>"AddOns-Ordner: … · Gefundene Daten-Dateien: 1", plus every read error.</summary>
    public static string EmptyDetail(AddonDataSource source)
    {
        var lines = new List<string>
        {
            source.Folder is { } folder
                ? string.Format(CultureInfo.CurrentCulture, Strings.DetailFolder, folder)
                : Strings.DetailFolderAuto,
            source.Files.Count == 0
                ? Strings.DetailNoFiles
                : string.Format(CultureInfo.CurrentCulture, Strings.DetailFiles, string.Join(", ", source.Files)),
        };
        lines.AddRange(source.Errors);
        lines.Add(Strings.DetailDiagnose);
        return string.Join("\n", lines);
    }

    /// <summary>The character the week is shown for: the one played last.</summary>
    private static AddonCharacter? Active(AddonData data) => data.Characters.MaxBy(c => c.UpdatedAt);

    /// <param name="levelsAbove">How far above the highest level timed the goal may plan (AppSettings.PlanLevelsAbove).</param>
    /// <param name="mdt">Mythic Dungeon Tools: the route chosen there for the key's dungeon goes on "Nächster Lauf".</param>
    public static OverviewViewModel Overview(AddonData data, DateTimeOffset now, int levelsAbove, MdtData mdt, string? emptyDetail = null)
    {
        var active = Active(data);
        if (active is null || data.Maps.Count == 0)
        {
            return new OverviewViewModel { EmptyDetail = emptyDetail };
        }
        var weekStart = data.WeekStart(now);
        TimeSpan? untilReset = weekStart is { } start ? start + TimeSpan.FromDays(7) - now : null;
        var ownKey = CurrentKey(data, active, weekStart);

        var highestTimed = HighestTimed(active);
        var rows = data.Maps.Values
            .OrderBy(m => m.Name, StringComparer.CurrentCulture)
            .Select(m => active.Best.TryGetValue(m.MapId, out var run)
                ? new DungeonRowViewModel(m.MapId, m.Name, m.TimeLimitSec, run.Level, run.DurationSec, run.OverTime, run.Score, ownKey, highestTimed)
                : new DungeonRowViewModel(m.MapId, m.Name, m.TimeLimitSec, null, null, false, 0, ownKey, highestTimed))
            .ToList();
        var inTime = active.Best.Count(b => !b.Value.OverTime && data.Maps.ContainsKey(b.Key));
        var bests = data.Maps.Keys.ToDictionary(id => id, id => BestScore(active, id));

        return new OverviewViewModel(
            untilReset, active.Rating, inTime, data.Maps.Count,
            new NextRun(ownKey, SelectedRoute(data, mdt, ownKey?.MapId),
                ownKey is null ? null : Show.Gain(ownKey.Level, BestScore(active, ownKey.MapId))),
            target => Goal(active.Rating, target, bests, highestTimed + levelsAbove, id => DungeonName(data, id)),
            rows,
            character: active.Name,
            incomplete: active.IsIncomplete,
            raiderIoUrl: WebLinks.RaiderIo(data.Region ?? "eu", WebLinks.RealmSlug(active.Realm), active.Name));
    }

    /// <summary>"Routen": every route of a season dungeon, by dungeon, the one chosen in MDT first.</summary>
    /// <param name="raidex">With the routes stored in Raidex; not in the preview, which shows no real account.</param>
    public static RoutesViewModel Routes(AddonData data, MdtData mdt, bool raidex = true)
    {
        var rows = mdt.Routes
            .Select(r => (Route: r, Dungeon: mdt.DungeonOf(r)))
            .Where(r => r.Dungeon is not null && (data.Maps.Count == 0 || data.Maps.ContainsKey(r.Dungeon.MapId)))
            .Select(r => (r.Route, Dungeon: r.Dungeon!, Name: RouteDungeonName(data, r.Dungeon!)))
            .OrderBy(r => r.Name, StringComparer.CurrentCulture)
            .ThenByDescending(r => r.Route.Selected)
            .ThenBy(r => r.Route.Name ?? Strings.RouteDefault, StringComparer.CurrentCulture)
            .Select(r => new RouteRowViewModel(r.Dungeon.MapId, r.Name, r.Route.Name ?? Strings.RouteDefault, r.Route.Selected,
                RouteForces.Of(r.Route, r.Dungeon), RouteMap.Of(r.Route, r.Dungeon))
            {
                Exported = Exporter(data, mdt, r.Route),
                ToSave = Saver(mdt, r.Route, r.Dungeon),
            })
            .ToList();
        return new RoutesViewModel(rows, mdt.Installed, text => Pasted(data, mdt, text),
            raidex ? stored => StoredRow(data, mdt, stored) : null);
    }

    /// <summary>What saving a route of the player's own MDT in Raidex sends: its export string and what the table shows of it.</summary>
    private static Func<RouteToSave?>? Saver(MdtData mdt, MdtRoute route, MdtDungeon dungeon) =>
        route.Preset is { } preset
            ? () => new RouteToSave(MdtString.WithoutAuthor(preset, mdt.Version), route.Name ?? Strings.RouteDefault,
                route.Uid, dungeon.MapId, route.Pulls.Count, RouteForces.Of(route, dungeon).Percent)
            : null;

    /// <summary>
    /// Hands a route from the player's MDT out as an export string, the way
    /// MDT itself would write it: its preset, plus the installed MDT's version
    /// and the character exporting it. Null where there is no preset to write.
    /// </summary>
    private static Func<string?>? Exporter(AddonData data, MdtData mdt, MdtRoute route)
    {
        if (route.Preset is not { } preset)
        {
            return null;
        }
        var me = Active(data);
        return () => MdtString.Export(preset, mdt.Version, me?.Name, me?.Realm, me?.Class is { } c ? (int)c : null);
    }

    /// <summary>
    /// A route stored in Raidex: decoded and drawn like the player's own when
    /// the installed MDT knows its dungeon, else by what Raidex stored with it.
    /// The first line says who sees it.
    /// </summary>
    internal static RouteRowViewModel StoredRow(AddonData data, MdtData mdt, StoredRoute stored)
    {
        var who = new DetailLine(Strings.DetailStored, (stored.Mine, stored.SharedWithGuild) switch
        {
            (true, false) => Strings.StoredOnlyYou,
            (true, true) => string.Format(CultureInfo.CurrentCulture, Strings.StoredForGuild, stored.GuildName),
            _ => string.Format(CultureInfo.CurrentCulture, Strings.StoredFromMember, stored.Owner, stored.GuildName),
        });
        try
        {
            var route = MdtString.Decode(stored.Mdt);
            if (mdt.DungeonOf(route) is { } dungeon)
            {
                return new RouteRowViewModel(dungeon.MapId, RouteDungeonName(data, dungeon), stored.Name, selected: false,
                    RouteForces.Of(route, dungeon), RouteMap.Of(route, dungeon), [who])
                { Stored = stored };
            }
        }
        catch (FormatException)
        {
            // Stored as sent; shown by what Raidex keeps with it.
        }
        var name = data.Maps.TryGetValue(stored.MapId, out var map) ? map.Name : DungeonCatalog.Find(stored.MapId)?.Name ?? $"#{stored.MapId}";
        return new RouteRowViewModel(stored.MapId, name, stored.Name, stored.Pulls, stored.Forces, [who]) { Stored = stored };
    }

    /// <summary>A pasted MDT string: the route with its forces, or why it shows none.</summary>
    internal static PastedRoute Pasted(AddonData data, MdtData mdt, string text)
    {
        if (!MdtString.LooksLikeOne(text))
        {
            return new PastedRoute(null, Strings.PasteNotMdt);
        }
        MdtRoute route;
        try
        {
            route = MdtString.Decode(text);
        }
        catch (FormatException e)
        {
            return new PastedRoute(null, Strings.PasteBroken, e.Message);
        }
        var name = route.Name ?? Strings.RouteDefault;
        if (mdt.DungeonOf(route) is not { } dungeon)
        {
            return new PastedRoute(null, string.Format(CultureInfo.CurrentCulture,
                mdt.Installed ? Strings.PasteUnknownDungeon : Strings.PasteNoMdt, name, route.Pulls.Count));
        }
        var known = route.Uid is not null && mdt.Routes.Any(r => r.Uid == route.Uid);
        var forces = RouteForces.Of(route, dungeon);
        var row = new RouteRowViewModel(dungeon.MapId, RouteDungeonName(data, dungeon), name, selected: false,
            forces, RouteMap.Of(route, dungeon))
        { IsExpanded = true };
        return new PastedRoute(row, known ? Strings.PasteKnown : Strings.PasteNew, Save:
            new RouteToSave(route.Preset is { } preset ? MdtString.WithoutAuthor(preset, null) : MdtString.Clean(text),
                name, route.Uid, dungeon.MapId, route.Pulls.Count, forces.Percent));
    }

    /// <summary>The route chosen in MDT for a dungeon, reduced to the card's line.</summary>
    private static RouteSummary? SelectedRoute(AddonData data, MdtData mdt, int? mapId)
    {
        if (mapId is null)
        {
            return null;
        }
        foreach (var route in mdt.Routes.Where(r => r.Selected))
        {
            if (mdt.DungeonOf(route) is { } dungeon && dungeon.MapId == mapId)
            {
                var forces = RouteForces.Of(route, dungeon);
                return new RouteSummary(RouteDungeonName(data, dungeon), route.Name ?? Strings.RouteDefault,
                    forces.Percent, forces.Pulls.Count);
            }
        }
        return null;
    }

    /// <summary>The dungeon's name in the game's language, from the addon; MDT's English one otherwise.</summary>
    private static string RouteDungeonName(AddonData data, MdtDungeon dungeon) =>
        data.Maps.TryGetValue(dungeon.MapId, out var map) ? map.Name : dungeon.EnglishName;

    public static KeystonesViewModel Keystones(AddonData data, DateTimeOffset now, string? emptyDetail = null)
    {
        var weekStart = data.WeekStart(now);
        var region = data.Region ?? "eu";
        var rows = data.Characters
            .OrderByDescending(c => c.Rating).ThenBy(c => c.Name, StringComparer.CurrentCulture)
            .Select(c =>
            {
                var key = CurrentKey(data, c, weekStart);
                var details = new List<DetailLine>();
                if (key is not null)
                {
                    details.Add(new DetailLine(Strings.DetailBest, BestIn(data, c, key.MapId)));
                    details.Add(new DetailLine(Strings.DetailGain, Show.Gain(key.Level, BestScore(c, key.MapId))));
                }
                var vault = c.VaultIn(weekStart);
                var waiting = c.RewardsWaiting(weekStart);
                if (waiting > 0)
                {
                    details.Add(new DetailLine(Strings.DetailVault,
                        string.Format(CultureInfo.CurrentCulture, Strings.VaultWaiting, waiting, c.Vault.Count)));
                }
                else if (vault.Count > 0)
                {
                    details.AddRange(VaultLines(vault));
                    // A snapshot of an earlier week has no runs of this one.
                    if (c.WeekRuns.Count > 0 && (weekStart is not { } start || c.UpdatedAt >= start))
                    {
                        details.Add(new DetailLine(Strings.DetailWeekRuns, string.Join(", ", c.WeekRuns.Select(r => $"+{r.Level}"))));
                    }
                }
                details.Add(new DetailLine(Strings.DetailSaved, Show.Moment(c.UpdatedAt)));
                return new KeyRowViewModel(c.Name, c.Class, region, c.Realm, c.Realm.Replace(" ", "", StringComparison.Ordinal),
                    key, c.Rating, TimeLimit(data, key), details,
                    vault.Select((s, i) => VaultPip.From(i + 1, s)).ToList(), rewardWaiting: waiting > 0);
            })
            .ToList();
        DateTimeOffset? asOf = data.Characters.Count > 0 ? data.Characters.Max(c => c.UpdatedAt) : null;
        return new KeystonesViewModel(rows, asOf) { EmptyDetail = emptyDetail };
    }

    /// <summary>A guild key as the page shows it: seen by the addon here, or reported to Raidex by a member.</summary>
    /// <param name="LinkRealm">The realm as Raider.IO takes it: spaceless (guild keys) or Blizzard's slug (Raidex).</param>
    /// <param name="ReportedBy">Who reported it to Raidex; null when this addon saw it itself.</param>
    private sealed record GuildEntry(string Name, string LinkRealm, string? RealmName, WowClass? Class, int MapId, int Level,
        int Rating, DateTimeOffset ObservedAt, string? ReportedBy, string? Spec, int? ItemLevel, string? Role = null,
        string? SpecIcon = null);

    /// <summary>
    /// "Gilde": the guild keys this week - those the addon heard in game, and,
    /// logged in to Raidex, those its members reported (plan 7.2). Per
    /// character the newer observation wins; Raidex's roster adds class, spec
    /// and item level.
    /// </summary>
    /// <param name="board">The active guild's keys in Raidex; null while logged out.</param>
    public static GuildViewModel Guild(AddonData data, DateTimeOffset now, GuildBoard? board = null)
    {
        var region = data.Region ?? "eu";
        var active = Active(data);
        var rows = Entries(data, now, board).Values
            .OrderByDescending(e => e.Level).ThenByDescending(e => e.Rating)
            .Select(e =>
            {
                var key = new Keystone(e.MapId, DungeonName(data, e.MapId), e.Level);
                var details = new List<DetailLine>();
                if (active is not null)
                {
                    // What the key is worth to the one looking: the own best there.
                    details.Add(new DetailLine(string.Format(CultureInfo.CurrentCulture, Strings.DetailYourBest, active.Name),
                        BestIn(data, active, key.MapId)));
                    details.Add(new DetailLine(string.Format(CultureInfo.CurrentCulture, Strings.DetailGainFor, active.Name),
                        Show.Gain(key.Level, BestScore(active, key.MapId))));
                }
                if (e.Spec is not null || e.ItemLevel is not null)
                {
                    details.Add(new DetailLine(Strings.DetailSpec, (e.Spec, e.ItemLevel) switch
                    {
                        ({ } spec, { } ilvl) => string.Format(CultureInfo.CurrentCulture, Strings.SpecIlvl, spec, ilvl),
                        ({ } spec, null) => spec,
                        (null, var ilvl) => string.Format(CultureInfo.CurrentCulture, Strings.IlvlOnly, ilvl),
                    }));
                }
                details.Add(e.ReportedBy is { } by
                    ? new DetailLine(Strings.DetailReported, string.Format(CultureInfo.CurrentCulture, Strings.ReportedBy, Show.Moment(e.ObservedAt), by))
                    : new DetailLine(Strings.DetailSeen, Show.Moment(e.ObservedAt)));
                return new KeyRowViewModel(e.Name, e.Class, region, e.RealmName, e.LinkRealm, key, e.Rating, TimeLimit(data, key), details,
                    spec: e.Spec, specIcon: e.SpecIcon, role: GroupPlannerViewModel.RoleOf(e.Role));
            })
            .ToList();
        var guildNames = data.Guilds.Select(g => g.Name).Append(board?.Guild).OfType<string>()
            .Where(n => n.Length > 0).Distinct(StringComparer.CurrentCulture).ToList();
        return new GuildViewModel(rows,
            guildName: guildNames.Count > 0 ? string.Join(", ", guildNames) : null,
            seenGuild: data.Guilds.Count > 0 || board is not null);
    }

    /// <summary>
    /// One entry per guild member with a key this week: what the addon heard
    /// in game and what Raidex reported, the newer observation winning, with
    /// class, spec and item level from Raidex's roster.
    /// </summary>
    private static Dictionary<string, GuildEntry> Entries(AddonData data, DateTimeOffset now, GuildBoard? board)
    {
        var weekStart = data.WeekStart(now) ?? DateTimeOffset.MinValue;
        // Guild keys name characters "Name-Realm" with the realm's spaces
        // removed; the own characters carry the realm as the game shows it.
        var own = data.Characters
            .GroupBy(c => Who(c.Name, c.Realm), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var entries = new Dictionary<string, GuildEntry>(StringComparer.Ordinal);
        foreach (var k in data.Guilds.SelectMany(g => g.Keys).Where(k => k.Level is > 0 && k.MapId is { } && k.SeenAt >= weekStart))
        {
            var realm = k.Character.Contains('-') ? k.Character.Split('-', 2)[1] : "";
            var who = Who(k.Name, realm);
            var mine = own.GetValueOrDefault(who);
            Keep(entries, who, new GuildEntry(k.Name, realm, mine?.Realm, mine?.Class, k.MapId!.Value, k.Level!.Value, k.Rating,
                k.SeenAt, null, null, null));
        }
        foreach (var k in board?.Keys ?? [])
        {
            var who = Who(k.Name, k.RealmSlug);
            Keep(entries, who, new GuildEntry(k.Name, k.RealmSlug, own.GetValueOrDefault(who)?.Realm,
                k.ClassId is >= 1 and <= 13 ? (WowClass)k.ClassId : null, k.MapId, k.Level, k.Rating ?? 0,
                DateTimeOffset.FromUnixTimeSeconds(k.ObservedAt), k.ReportedBy, k.Spec, k.ItemLevel, k.Role, k.SpecIcon));
        }
        return entries;
    }

    /// <summary>
    /// "Gruppenplaner", the page of its own (plan 7.2b): everyone on the guild
    /// board as a candidate, and what a key would pay them from Raidex.
    /// </summary>
    /// <param name="lookup">
    /// Where a member's season comes from; the Raidex account when null, which
    /// is also what says whether the planner can ask at all.
    /// </param>
    public static GroupPlannerViewModel Planner(AddonData data, DateTimeOffset now, GuildBoard? board = null,
        Func<PlannerPick, Task<MplusCharacter?>>? lookup = null)
    {
        // A character of this account is on the board like anyone else, but the
        // addon knows its runs first-hand - better than anything Raidex can say.
        var own = data.Characters
            .GroupBy(c => Who(c.Name, c.Realm), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<int, double>)g.First().Best
                .ToDictionary(b => b.Key, b => b.Value.Score), StringComparer.Ordinal);
        var picks = Entries(data, now, board).Values
            .OrderByDescending(e => e.Level).ThenByDescending(e => e.Rating).ThenBy(e => e.Name, StringComparer.CurrentCulture)
            .Select(e => new PlannerPick(Who(e.Name, e.LinkRealm), e.Name, e.LinkRealm, e.Class,
                e.MapId, e.Level, DungeonName(data, e.MapId), e.Rating, e.Spec,
                own.GetValueOrDefault(Who(e.Name, e.LinkRealm)), e.Role, e.SpecIcon))
            .ToList();
        var account = Raidex.RaidexAccount.Instance;
        return new GroupPlannerViewModel(picks, id => DungeonName(data, id), data.RecommendedItemLevel,
            knowsRewards: data.Rewards.Count > 0,
            isLoggedIn: lookup is not null || account.Me is not null,
            lookup ?? (pick => account.MplusAsync(pick.RealmSlug, pick.Name)),
            problem: lookup is null ? () => account.Problem : null);
    }

    /// <summary>A character across spellings of its realm: "name:realmletters" (Raidex's realm_key).</summary>
    private static string Who(string name, string realm) => name.ToLowerInvariant() + ":" + KeysReport.RealmKey(realm);

    /// <summary>Keeps the newer of two observations of one character; the roster's spec and item level either way.</summary>
    private static void Keep(Dictionary<string, GuildEntry> entries, string who, GuildEntry entry)
    {
        if (!entries.TryGetValue(who, out var old))
        {
            entries[who] = entry;
            return;
        }
        var newer = entry.ObservedAt > old.ObservedAt ? entry : old;
        var other = ReferenceEquals(newer, entry) ? old : entry;
        entries[who] = newer with
        {
            Class = newer.Class ?? other.Class,
            RealmName = newer.RealmName ?? other.RealmName,
            Spec = newer.Spec ?? other.Spec,
            ItemLevel = newer.ItemLevel ?? other.ItemLevel,
            Role = newer.Role ?? other.Role,
            SpecIcon = newer.SpecIcon ?? other.SpecIcon,
        };
    }

    /// <summary>The goal card: the runs to <paramref name="target"/>, or null when it is out of reach.</summary>
    /// <param name="bests">The best score per season dungeon, 0 where none.</param>
    /// <param name="levelCap">The level the plan stays at or below while it can (RatingPlanner.Plan).</param>
    public static RatingGoal? Goal(int rating, int target, IReadOnlyDictionary<int, double> bests, int levelCap,
        Func<int, string> dungeonName)
    {
        if (RatingPlanner.Plan(rating, target, bests, levelCap) is not { } plan)
        {
            return null;
        }
        const int shown = 4;
        var suggestion = string.Join(", ", plan.Take(shown).Select(r => $"{dungeonName(r.MapId)} +{r.Level}"));
        if (plan.Count > shown)
        {
            suggestion += " " + string.Format(CultureInfo.CurrentCulture, Strings.MoreRuns, plan.Count - shown);
        }
        var from = rating / 100 * 100;
        var runs = plan.Select(r => new GoalRun(dungeonName(r.MapId), r.Level, r.Gain)).ToList();
        return new RatingGoal(target, runs, suggestion, 100.0 * (rating - from) / (target - from));
    }

    private static double BestScore(AddonCharacter c, int mapId) =>
        c.Best.TryGetValue(mapId, out var run) ? run.Score : 0;

    private static int HighestTimed(AddonCharacter c) =>
        c.Best.Values.Where(r => !r.OverTime).Select(r => r.Level).DefaultIfEmpty(0).Max();

    /// <summary>"+11 in 28:22 · 1:38 unter der Zeit · 285 Punkte", or "noch nicht gelaufen".</summary>
    private static string BestIn(AddonData data, AddonCharacter c, int mapId) =>
        c.Best.TryGetValue(mapId, out var run)
            ? Show.Run(run.Level, run.DurationSec, run.OverTime, data.Maps.GetValueOrDefault(mapId)?.TimeLimitSec, run.Score)
            : Strings.NotRunYet;

    /// <summary>One line per slot, the label on the first only.</summary>
    internal static IEnumerable<DetailLine> VaultLines(IReadOnlyList<VaultSlot> slots) =>
        slots.Select((s, i) => new DetailLine(i == 0 ? Strings.DetailVault : "", Show.VaultSlot(i + 1, s)));

    private static int? TimeLimit(AddonData data, Keystone? key) =>
        key is null ? null : data.Maps.GetValueOrDefault(key.MapId)?.TimeLimitSec;

    /// <summary>The character's keystone - only if it was seen this week.</summary>
    /// <remarks>Keys are replaced at the weekly reset; one saved before it is gone.</remarks>
    private static Keystone? CurrentKey(AddonData data, AddonCharacter c, DateTimeOffset? weekStart)
    {
        if (c.KeyMapId is not { } mapId || c.KeyLevel is not { } level || level <= 0)
        {
            return null;
        }
        if (weekStart is { } start && c.UpdatedAt < start)
        {
            return null;
        }
        return new Keystone(mapId, DungeonName(data, mapId), level);
    }

    private static string DungeonName(AddonData data, int mapId) =>
        data.Maps.TryGetValue(mapId, out var map) ? map.Name : $"#{mapId}";
}
