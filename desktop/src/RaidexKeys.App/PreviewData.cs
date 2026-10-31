// SPDX-FileCopyrightText: 2026 lasse tech, Lars Gossard
// SPDX-License-Identifier: MIT

using System.Globalization;
using RaidexKeys.App.ViewModels;
using RaidexKeys.Core;
using RaidexKeys.Core.Mdt;
using RaidexKeys.Core.Raidex;
using RaidexKeys.Core.Rating;
using RaidexKeys.Core.SavedVariables;

namespace RaidexKeys.App;

/// <summary>
/// The sample data for <c>--preview</c> and the AppStream screenshots
/// (<c>--screenshot</c>): made-up players on the real season.
/// </summary>
/// <remarks>
/// The dungeons are the season's eight (Midnight season 2), with the game's
/// names in German and English (its MapChallengeMode table) and Raider.IO's
/// timers. Every run is timed against its dungeon's timer and scored with the
/// game's formula (KeystoneRating), and a character's rating is the sum of its
/// best runs - so rows, goal and gains add up as with real data. The data goes
/// through the same LiveData the real window uses. Characters, guild and
/// routes are made up; the characters and levels follow the brand guide.
/// The specs carry no icon: Blizzard's are real addresses on their CDN, and
/// made-up ones would be broken links - the drawn role glyphs stand in, as
/// they do for a guild Raidex has not scanned since it learned the spec ids.
/// The language is set before the preview is built, and a change restarts
/// the app (Program, Language).
/// </remarks>
internal static class PreviewData
{
    private const string Realm = "Blackrock";
    private const string Guild = "Nachtwache";

    private static readonly bool German = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";

    private static string Say(string german, string english) => German ? german : english;

    // Midnight season 2, by challenge-mode id: German and English as the game names them.
    private const int AltarOfFangs = 588, DenOfNalorakk = 586, KingsRest = 249, MurderRow = 587,
        RubyLifePools = 399, Sethraliss = 250, BlindingVale = 584, VoidscarArena = 585;

    private static readonly IReadOnlyDictionary<int, DungeonInfo> Dungeons = new[]
    {
        new DungeonInfo(AltarOfFangs, Say("Altar der Fänge", "Altar of Fangs"), 1800),
        new DungeonInfo(DenOfNalorakk, Say("Nalorakks Bau", "Den of Nalorakk"), 1920),
        new DungeonInfo(KingsRest, Say("Die Königsruh", "Kings' Rest"), 1980),
        new DungeonInfo(MurderRow, Say("Mördergasse", "Murder Row"), 2040),
        new DungeonInfo(RubyLifePools, Say("Rubinlebensbecken", "Ruby Life Pools"), 1680),
        new DungeonInfo(Sethraliss, Say("Der Tempel von Sethraliss", "Temple of Sethraliss"), 1920),
        new DungeonInfo(BlindingVale, Say("Das blendende Tal", "The Blinding Vale"), 1800),
        new DungeonInfo(VoidscarArena, Say("Arena der Leerennarbe", "Voidscar Arena"), 1800),
    }.ToDictionary(d => d.MapId);

    /// <summary>Best runs as (dungeon, level, minutes, seconds): timed or over time by the dungeon's timer, scored as the game does.</summary>
    private static Dictionary<int, BestRun> Bests(params (int MapId, int Level, int Min, int Sec)[] runs) =>
        runs.ToDictionary(r => r.MapId, r =>
        {
            var duration = r.Min * 60 + r.Sec;
            var limit = Dungeons[r.MapId].TimeLimitSec;
            return new BestRun(r.Level, duration, duration > limit, Math.Round(KeystoneRating.Score(r.Level, duration, limit), 1));
        });

    /// <summary>
    /// What a key level rewards, as the addon reads it from the game: made up
    /// here, but ramped like the game's own table, so the group planner's item
    /// level switch has something to go by.
    /// </summary>
    private static Dictionary<int, RewardLevel> Rewards() =>
        Enumerable.Range(2, 19).ToDictionary(level => level,
            level => new RewardLevel(Math.Min(311, 270 + 4 * (level - 2)), Math.Min(318, 277 + 4 * (level - 2))));

    /// <summary>The vault's dungeon row after the week's runs: slot 1, 4 and 8, each at the level of its run.</summary>
    private static VaultSlot[] Vault(IReadOnlyList<WeekRun> week)
    {
        var levels = week.Select(r => r.Level).OrderDescending().ToList();
        return [.. new[] { 1, 4, 8 }.Select(t => new VaultSlot(t, levels.Count, levels.Count >= t ? levels[t - 1] : 0))];
    }

    private static AddonCharacter Character(string name, WowClass wowClass, int keyMapId, int keyLevel,
        DateTimeOffset seen, Dictionary<int, BestRun> best, WeekRun[] week) =>
        new($"Player-1403-{name}", name, Realm, wowClass, 90, keyMapId, keyLevel,
            (int)Math.Round(best.Values.Sum(b => b.Score)), best, Vault(week), seen, week);

    /// <summary>
    /// The account's four characters of the brand guide on the real season.
    /// The own key is a +12 where the best is a +10, so "Nächster Lauf"
    /// shows what timing it adds.
    /// </summary>
    private static AddonData Seed(DateTimeOffset now) => new()
    {
        Region = "eu",
        Season = 18,
        // "Reset in 3 Tagen, 11 Std", as in the guide.
        ResetAt = now + new TimeSpan(3, 11, 0, 0),
        Maps = Dungeons,
        Rewards = Rewards(),
        Characters =
        [
            Character("Raidex", WowClass.DeathKnight, Sethraliss, 12, now.AddHours(-2),
                Bests((AltarOfFangs, 12, 28, 41), (DenOfNalorakk, 11, 30, 12), (KingsRest, 11, 31, 40), (MurderRow, 10, 31, 7),
                    (RubyLifePools, 11, 26, 5), (Sethraliss, 10, 29, 30), (BlindingVale, 11, 33, 52), (VoidscarArena, 10, 31, 20)),
                [new(AltarOfFangs, 12, true), new(KingsRest, 11, true), new(RubyLifePools, 11, true)]),
            Character("Frostbann", WowClass.Mage, MurderRow, 10, now.AddDays(-1),
                Bests((AltarOfFangs, 10, 27, 10), (DenOfNalorakk, 10, 29, 40), (KingsRest, 9, 30, 5), (MurderRow, 9, 32, 40),
                    (RubyLifePools, 10, 25, 20), (Sethraliss, 9, 28, 0), (BlindingVale, 9, 31, 50), (VoidscarArena, 8, 27, 45)),
                [new(MurderRow, 10, false), new(Sethraliss, 9, true), new(VoidscarArena, 9, true), new(AltarOfFangs, 8, true)]),
            Character("Tarnkralle", WowClass.Hunter, VoidscarArena, 8, now.AddDays(-2),
                Bests((AltarOfFangs, 8, 26, 30), (DenOfNalorakk, 8, 28, 15), (KingsRest, 7, 29, 50), (MurderRow, 8, 33, 5),
                    (RubyLifePools, 8, 24, 40), (Sethraliss, 7, 27, 55), (BlindingVale, 7, 30, 40), (VoidscarArena, 7, 31, 10)),
                [new(KingsRest, 8, true), new(DenOfNalorakk, 7, true)]),
            Character("Donnerhuf", WowClass.Druid, DenOfNalorakk, 7, now.AddDays(-3),
                Bests((AltarOfFangs, 7, 27, 20), (DenOfNalorakk, 6, 28, 50), (KingsRest, 6, 30, 30), (MurderRow, 6, 32, 15),
                    (RubyLifePools, 7, 25, 35), (Sethraliss, 6, 29, 10), (VoidscarArena, 6, 31, 45)),
                [new(RubyLifePools, 7, true)]),
        ],
    };

    /// <summary>
    /// The guild board as Raidex hands it out when logged in: the members'
    /// keys with their class, reported by themselves; two of the account's
    /// characters among them.
    /// </summary>
    private static GuildBoard Board(AddonData data, DateTimeOffset now)
    {
        BoardKey Key(string name, WowClass wowClass, int mapId, int level, int rating, double hoursAgo) =>
            new(name, "blackrock", (int)wowClass, Members[name].Spec, Members[name].ItemLevel, mapId, level, rating, "self",
                now.AddHours(-hoursAgo).ToUnixTimeSeconds(), name, Members[name].Role);
        int Own(string name) => data.Characters.First(c => c.Name == name).Rating;
        return new GuildBoard(Guild,
        [
            Key("Nachtfeder", WowClass.Priest, KingsRest, 13, 2794, 5),
            Key("Raidex", WowClass.DeathKnight, Sethraliss, 12, Own("Raidex"), 2),
            Key("Eisenbart", WowClass.Warrior, AltarOfFangs, 11, 2533, 20),
            Key("Glutschwinge", WowClass.Evoker, RubyLifePools, 11, 2498, 9),
            Key("Frostbann", WowClass.Mage, MurderRow, 10, Own("Frostbann"), 24),
            Key("Schattenweb", WowClass.Rogue, BlindingVale, 9, 2172, 30),
        ]);
    }

    /// <summary>
    /// What Raidex would answer about a guild member for the group planner:
    /// the role their class plays here, an item level around the season's
    /// rewards (Midnight's scale, after the squish), and a best run per
    /// dungeon below the rating they show - so
    /// every key on the board is worth something to everybody.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Role, string Spec, int ItemLevel, int Best)> Members =
        new Dictionary<string, (string, string, int, int)>(StringComparer.Ordinal)
        {
            ["Nachtfeder"] = ("healer", Say("Heilig", "Holy"), 314, 12),
            ["Raidex"] = ("tank", Say("Blut", "Blood"), 311, 11),
            ["Eisenbart"] = ("tank", Say("Schutz", "Protection"), 308, 10),
            ["Glutschwinge"] = ("damage", Say("Verheerung", "Devastation"), 311, 10),
            ["Frostbann"] = ("damage", Say("Frost", "Frost"), 304, 9),
            ["Schattenweb"] = ("damage", Say("Meucheln", "Assassination"), 300, 8),
            ["Tarnkralle"] = ("damage", Say("Tierherrschaft", "Beast Mastery"), 297, 8),
            ["Donnerhuf"] = ("healer", Say("Wiederherstellung", "Restoration"), 293, 7),
        };

    /// <summary>
    /// The season of one member, as <c>/mplus/character</c> hands it out:
    /// every season dungeon run right on the timer at their level, one lower
    /// in the two dungeons they like least - so the planner has something to
    /// weigh. Unknown names get nothing, as Raidex would answer for a
    /// character it never scanned.
    /// </summary>
    private static Task<MplusCharacter?> MemberSeason(PlannerPick pick)
    {
        if (!Members.TryGetValue(pick.Name, out var member))
        {
            return Task.FromResult<MplusCharacter?>(null);
        }
        var runs = Dungeons.Values.Select((dungeon, i) =>
        {
            var level = member.Best - (i % 4 == 0 ? 1 : 0);
            var duration = (int)(dungeon.TimeLimitSec * 0.94);
            return new MplusRun(dungeon.MapId, level, duration, true,
                Math.Round(KeystoneRating.Score(level, duration, dungeon.TimeLimitSec), 1));
        }).ToList();
        return Task.FromResult<MplusCharacter?>(new MplusCharacter(pick.Name, pick.RealmSlug, 18,
            (int)Math.Round(runs.Sum(r => r.Score)), null, null, member.Role, member.ItemLevel, runs));
    }

    /// <summary>
    /// Mythic Dungeon Tools as if installed: one made-up dungeon per route, its
    /// forces set so the route reaches <c>percent</c> in <c>pulls</c> pulls.
    /// No map tiles: the floor is looked up separately (below).
    /// </summary>
    /// <summary>
    /// Where the sample route's pulls stand, in MDT's map units (x to 840, y
    /// down to -555): a way over the walkable ground of The Blinding Vale,
    /// the dungeon the first sample route names - north along the rim, down
    /// the east side and back west through the crater. Picked off the floor
    /// itself, so the pulls sit on paths and not in cliffs.
    /// </summary>
    private static readonly (double X, double Y)[] Way =
    [
        (140, -144), (224, -111), (336, -83), (426, -128), (504, -167),
        (605, -233), (700, -233), (784, -230), (644, -333), (538, -389),
        (465, -311), (392, -344), (263, -278), (140, -389),
    ];

    /// <summary>The dungeon the sample route lies on, as MDT names its folder.</summary>
    private const string SampleFloor = "TheBlindingVale";

    /// <summary>
    /// The floor the sample route is drawn on, or null for no map at all: the
    /// folder chosen by hand, else the dungeon's own tiles from a Mythic
    /// Dungeon Tools installed here, else the schematic floor
    /// in <c>RAIDEXKEYS_PREVIEW_TILES_FALLBACK</c> for a machine without one.
    /// </summary>
    /// <remarks>
    /// The route's pulls were laid out on this dungeon's walkable ground, so
    /// it is this floor or a schematic one. An installed MDT is found rather
    /// than configured: the screenshot build used to need the folder pointed at
    /// the folder by hand, and a run that forgot it quietly shipped shots of
    /// the schematic floor - a black ground with faint boxes. Nothing of MDT
    /// is copied here; the tiles are read where they lie, as the app reads
    /// them for a real route.
    /// </remarks>
    public static string? TilesFolder()
    {
        var chosen = Environment.GetEnvironmentVariable("RAIDEXKEYS_PREVIEW_TILES");
        var fallback = Environment.GetEnvironmentVariable("RAIDEXKEYS_PREVIEW_TILES_FALLBACK");
        return new[] { chosen, InstalledTiles(), fallback }.FirstOrDefault(Directory.Exists);
    }

    /// <summary>The sample route's floor in an MDT installed on this machine.</summary>
    private static string? InstalledTiles()
        => WowLocator.FindAddOnsFolders()
            .Select(a => Path.Combine(a, "MythicDungeonTools"))
            .Where(Directory.Exists)
            .SelectMany(mdt => Directory.EnumerateDirectories(mdt)
                .Select(season => Path.Combine(season, "Textures", SampleFloor)))
            .FirstOrDefault(Directory.Exists);

    private static MdtData Mdt()
    {
        var dungeons = new Dictionary<int, MdtDungeon>();
        var routes = new List<MdtRoute>();
        var tiles = TilesFolder();
        var tileDirs = tiles is null
            ? new Dictionary<int, string>()
            : new Dictionary<int, string> { [1] = tiles };
        void Route(int mapId, string? name, bool selected, double percent, int pulls)
        {
            var index = dungeons.Count + 1;
            // Per pull two to four of the first enemy and one of the second, as a route pulls packs.
            var first = new Dictionary<int, MdtClone>();
            var second = new Dictionary<int, MdtClone>();
            var list = new List<MdtPull>();
            for (var i = 1; i <= pulls; i++)
            {
                var (x, y) = Way[(i - 1) % Way.Length];
                var scouts = Enumerable.Range(first.Count + 1, 2 + i % 3).ToList();
                // The pack around its point, a few units apart, as MDT places clones.
                scouts.ForEach(c => first[c] = new MdtClone(4, x + 9 * (c % 3) - 9, y - 9 * (c % 2), 1));
                second[i] = new MdtClone(9, x + 14, y - 16, 1);
                list.Add(new MdtPull(new Dictionary<int, IReadOnlyList<int>> { [1] = scouts, [2] = [i] }));
            }
            var total = first.Values.Sum(c => c.Count) + second.Values.Sum(c => c.Count);
            dungeons[index] = new MdtDungeon(index, mapId, Dungeons[mapId].Name, (int)Math.Round(total * 100 / percent),
                new Dictionary<int, MdtEnemy>
                {
                    [1] = new(Say("Späher", "Scout"), 1, first),
                    [2] = new(Say("Wächterin", "Warden"), 2, second),
                })
            { TileDirs = tileDirs };
            routes.Add(new MdtRoute(index, name, $"preview{index}", selected, list));
        }
        Route(Sethraliss, Say("Dreier-Pull", "Three-Pull"), true, 101.4, 14);
        Route(Sethraliss, null, false, 100.6, 12);
        Route(KingsRest, null, true, 100.8, 12);
        Route(MurderRow, Say("Boss-Skip", "Boss Skip"), true, 102.1, 11);
        Route(BlindingVale, Say("Schnell", "Fast"), true, 100.3, 13);
        return new MdtData(true, dungeons, routes);
    }

    public static ShellViewModel CreateShell(string section)
    {
        var now = DateTimeOffset.Now;
        var data = Seed(now);
        var mdt = Mdt();
        var routes = LiveData.Routes(data, mdt, raidex: false);
        // --expanded: the route on SampleFloor out, so its map shows in the
        // shot. That one and no other: the pulls were laid out on this floor,
        // and the table is sorted by the dungeon's name - which is localised,
        // so the first row is a different dungeon in German than in English
        // and the shot would name one dungeon while drawing another.
        if (Program.Expanded
            && routes.RoutesTable.Rows.FirstOrDefault(r => r.Dungeon == Dungeons[BlindingVale].Name)
                is { } onTheFloor)
        {
            onTheFloor.IsExpanded = true;
        }
        var board = Board(data, now);
        // The planner answers from the sample data, as Raidex would.
        var planner = LiveData.Planner(data, now, board, MemberSeason);
        Planned(planner);
        return new ShellViewModel(
            section,
            isPreview: true,
            overview: LiveData.Overview(data, now, levelsAbove: 1, mdt),
            keystones: LiveData.Keystones(data, now),
            // Without "In Raidex gespeichert": the preview shows no real account, as in the settings.
            routes: routes,
            guild: LiveData.Guild(data, now, board),
            planner: planner,
            calendar: Calendar(now));
    }

    private static CalendarViewModel Calendar(DateTimeOffset now)
    {
        var calendar = new CalendarViewModel(new PreviewCalendar());
        calendar.Game.Update(PreviewCalendar.GameRaids(now), [], now);
        return calendar;
    }

    /// <summary>
    /// The sample guild plans its evening right away: everybody is in, and the
    /// group stands - so the page shows what the planner does, screenshots
    /// included. The sample seasons answer without waiting, so the command is
    /// through by the time the window opens.
    /// </summary>
    private static void Planned(ViewModels.GroupPlannerViewModel planner)
    {
        foreach (var person in planner.People)
        {
            person.IsPicked = true;
        }
        planner.PlanCommand.Execute(null);
    }
}
